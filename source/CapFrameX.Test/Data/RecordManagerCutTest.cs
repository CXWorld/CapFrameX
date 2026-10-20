using CapFrameX.Capture.Contracts;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Test.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// "Cut record" (<see cref="RecordManager.DuplicateSession"/>) saves the part of a record inside or
    /// outside a time range as a new record, sensor data included.
    /// </summary>
    [TestClass]
    public class RecordManagerCutTest
    {
        private const double SensorInterval = 0.25;

        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordManagerCutTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task Cut_KeepsTheSensorSamplesInsideTheRange()
        {
            var session = SessionWithSensors(seconds: 10, sensorSamples: 40);

            var cut = await Cut(session, inverse: false, startTime: 2, endTime: 4);

            // Measure times 2.0 to 4.0 s are samples 8 to 16.
            CollectionAssert.AreEqual(Enumerable.Range(8, 9).Select(i => (double)i).ToArray(), SensorValues(cut, "CPU Package"));
            Assert.AreEqual(9, cut.Runs[0].SensorData2.MeasureTime.Values.Count);
        }

        [TestMethod]
        public async Task InverseCut_KeepsTheSensorSamplesOutsideTheRange()
        {
            var session = SessionWithSensors(seconds: 10, sensorSamples: 40);

            var cut = await Cut(session, inverse: true, startTime: 2, endTime: 4);

            var expected = Enumerable.Range(0, 40).Where(i => i < 8 || i > 16).Select(i => (double)i).ToArray();
            CollectionAssert.AreEqual(expected, SensorValues(cut, "CPU Package"));
        }

        [TestMethod]
        public async Task SensorMissingItsLastSlot_KeepsItsValuesAndPadsWithNaN()
        {
            var session = SessionWithSensors(seconds: 10, sensorSamples: 40);
            // A capture that stopped between AddMeasureTime and CompleteMeasure: the sensor misses the
            // last measure slot. Kept slots 8 to 16 include it.
            var shortSensor = new SessionSensorEntry("GPU Core", "Load");
            foreach (var value in Enumerable.Range(0, 16))
                shortSensor.Values.AddLast(value);
            ((SessionSensorData2)session.Runs[0].SensorData2).Add("GPU Core", shortSensor);

            var cut = await Cut(session, inverse: false, startTime: 2, endTime: 4);

            Assert.IsNotNull(cut, "the cut record was not written");
            CollectionAssert.AreEqual(Enumerable.Range(8, 9).Select(i => (double)i).ToArray(), SensorValues(cut, "CPU Package"));
            var expected = Enumerable.Range(8, 8).Select(i => (double)i).Append(double.NaN).ToArray();
            CollectionAssert.AreEqual(expected, SensorValues(cut, "GPU Core"));
        }

        [TestMethod]
        public async Task SensorWithoutValues_StaysEmpty()
        {
            var session = SessionWithSensors(seconds: 10, sensorSamples: 40);
            ((SessionSensorData2)session.Runs[0].SensorData2).Add("GPU Core", new SessionSensorEntry("GPU Core", "Load"));

            var cut = await Cut(session, inverse: false, startTime: 2, endTime: 4);

            Assert.AreEqual(0, SensorValues(cut, "GPU Core").Length);
        }

        [TestMethod]
        public async Task LongSensorLog_IsCutInLinearTime()
        {
            // 5 h at 250 ms. Copying each kept sample with ElementAt and normalizing the measure times
            // through BetweenMeasureTimes were both O(n²): together about 150 s here.
            const int samples = 72000;
            var session = SessionWithSensors(seconds: samples * SensorInterval, sensorSamples: samples, framesPerSecond: 1);

            var stopwatch = Stopwatch.StartNew();
            var cut = await Cut(session, inverse: false, startTime: 0, endTime: samples * SensorInterval);
            stopwatch.Stop();

            Assert.AreEqual(samples, SensorValues(cut, "CPU Package").Length);
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"cut took {stopwatch.Elapsed}");
        }

        private async Task<ISession> Cut(ISession session, bool inverse, double startTime, double endTime)
        {
            var observer = new Mock<IRecordDirectoryObserver>();
            observer.SetupGet(o => o.ObservingDirectoryStream).Returns(Observable.Return(new DirectoryInfo(_folder)));
            var manager = new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, observer.Object, null, null, null, null, null, new EventAggregator(), null, null);

            await manager.DuplicateSession(session, inverse, startTime, endTime);

            var files = Directory.GetFiles(_folder, "*.json");
            return files.Length == 1 ? manager.LoadData(files[0]) : null;
        }

        private static double[] SensorValues(ISession session, string name)
            => session.Runs[0].SensorData2[name].Values.ToArray();

        private static Session SessionWithSensors(double seconds, int sensorSamples, int framesPerSecond = 100)
        {
            var frametimes = Enumerable.Repeat(1000d / framesPerSecond, (int)(seconds * framesPerSecond)).ToArray();
            var run = RecordFiles.Run(frametimes);

            var sensorData = new SessionSensorData2();
            var cpuPackage = new SessionSensorEntry("CPU Package", "Power");
            sensorData.Add("CPU Package", cpuPackage);
            for (int i = 0; i < sensorSamples; i++)
            {
                sensorData.MeasureTime.Values.AddLast(i * SensorInterval);
                sensorData.BetweenMeasureTime.Values.AddLast(i == 0 ? 0 : SensorInterval);
                cpuPackage.Values.AddLast(i);
            }
            run.SensorData2 = sensorData;

            var session = RecordFiles.SessionWith("Game", run);
            session.Info.ProcessName = "Game.exe";
            return session;
        }
    }
}
