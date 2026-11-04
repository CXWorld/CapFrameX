using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Extensions;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class RecordManagerCacheTest
    {
        private string _testDirectory;
        private RecordManager _recordManager;

        [TestInitialize]
        public void Initialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordCacheTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _recordManager = CreateRecordManager();
        }

        private static RecordManager CreateRecordManager(ProcessList processList = null)
        {
            return new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null,
                null,
                null,
                null,
                null,
                processList,
                null,
                new EventAggregator(),
                null,
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [TestMethod]
        public void LoadData_UnchangedNormalizedPath_ReusesSession()
        {
            string path = CreateSessionFile("capture.json", "First.exe");
            string equivalentPath = Path.Combine(_testDirectory, ".", "capture.json");

            ISession first = _recordManager.LoadData(path);
            ISession second = _recordManager.LoadData(equivalentPath);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void LoadData_ChangedFileStamp_ReloadsSession()
        {
            string path = CreateSessionFile("capture.json", "First.exe");
            ISession first = _recordManager.LoadData(path);

            DateTime changedStamp = File.GetLastWriteTimeUtc(path).AddSeconds(2);
            File.WriteAllText(path, CreateSessionJson("Other.exe"));
            File.SetLastWriteTimeUtc(path, changedStamp);

            ISession second = _recordManager.LoadData(path);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.AreEqual("Other.exe", second.Info.ProcessName);
        }

        [TestMethod]
        public async Task LoadData_ConcurrentRequests_ShareSession()
        {
            string path = CreateSessionFile("capture.json", "First.exe");
            var loads = Enumerable.Range(0, 12)
                .Select(_ => Task.Run(() => _recordManager.LoadData(path)))
                .ToArray();

            ISession[] sessions = await Task.WhenAll(loads);

            Assert.IsTrue(sessions.All(session => session != null));
            Assert.IsTrue(sessions.All(session => ReferenceEquals(sessions[0], session)));
        }

        [TestMethod]
        public async Task UpdateCustomData_JsonInvalidatesSessionAndMetadataCaches()
        {
            string path = CreateMetadataSessionFile("update.json", "Update.exe", "Update");
            var session = _recordManager.LoadData(path);
            var recordInfo = await _recordManager.GetFileRecordInfo(new FileInfo(path));

            Assert.IsNotNull(session);
            Assert.IsNotNull(recordInfo);
            Assert.AreEqual(1, GetPrivateCacheCount("_sessionCache"));
            Assert.AreEqual(1, GetPrivateCacheCount("_recordInfoCache"));

            _recordManager.UpdateCustomData(recordInfo, "CPU", "GPU", "RAM", "Mainboard", "Updated", "Comment");

            Assert.AreEqual(0, GetPrivateCacheCount("_sessionCache"));
            Assert.AreEqual(0, GetPrivateCacheCount("_recordInfoCache"));
        }

        [TestMethod]
        public void IncrementalPresentHash_MatchesLegacyContract()
        {
            var cases = new[]
            {
                new string[0],
                new[] { "single" },
                new[] { "first", "second", "third" },
                new[] { "first", string.Empty, "third" },
                new[] { "embedded,comma", "\u00e4\ud83d\ude80" }
            };
            var method = typeof(RecordManager).GetMethod("GetPresentLinesSha1",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            foreach (var lines in cases)
            {
                string expected = string.Join(",", lines).GetSha1();
                string actual = (string)method.Invoke(null, new object[] { lines });
                Assert.AreEqual(expected, actual);
            }
            Assert.AreEqual("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709",
                (string)method.Invoke(null, new object[] { new string[0] }));
        }

        [TestMethod]
        public void ConvertPresentData_DominantSwapChain_PreservesSelectedRowsAndValues()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new EventAggregator(),
                new MockCaptureService(),
                null);
            const string header = "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange";
            var selectedRows = new[]
            {
                "P2,2,C,10.00,20,21",
                "P2,2,C,10.02,22,23",
                "P2,2,C,10.04,24,25",
                "P2,2,C,10.06,26,27"
            };
            var lines = new List<string> { header };
            lines.AddRange(new[]
            {
                "P1,1,A,1.00,10,11",
                "P1,1,A,1.01,10,11",
                "P1,1,A,1.02,10,11",
                "P1,1,B,2.00,12,13",
                "P1,1,B,2.01,12,13"
            });
            lines.AddRange(selectedRows);

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            Assert.AreEqual(string.Join(",", selectedRows).GetSha1(), run.Hash);
            CollectionAssert.AreEqual(new[] { 20d, 22d, 24d, 26d }, run.CaptureData.MsBetweenPresents);
            CollectionAssert.AreEqual(new[] { 21d, 23d, 25d, 27d }, run.CaptureData.MsBetweenDisplayChange);
            var expectedTimes = new[] { 0d, 0.02d, 0.04d, 0.06d };
            for (int i = 0; i < expectedTimes.Length; i++)
            {
                Assert.AreEqual(expectedTimes[i], run.CaptureData.TimeInSeconds[i], 1E-12);
            }
            Assert.IsTrue(run.CaptureData.AnimationError.All(double.IsNaN));
        }

        [TestMethod]
        public async Task GetFileRecordInfo_JsonMetadataScan_MatchesFullSessionAndCachesResult()
        {
            string path = Path.Combine(_testDirectory, "metadata.json");
            File.WriteAllText(path,
                "{\"Hash\":\"SESSION-HASH\",\"Info\":{" +
                "\"Id\":\"8f9f8c95-f1c1-4a38-90b4-61c74a40df35\"," +
                "\"CreationDate\":\"2026-07-18T12:00:00Z\",\"ProcessName\":\"Game.exe\"," +
                "\"GameName\":\"Game Display\",\"Processor\":\"CPU\",\"GPU\":\"GPU\"," +
                "\"SystemRam\":\"32 GB\",\"Comment\":\"metadata\"}," +
                "\"Runs\":[" +
                "{\"CaptureData\":{\"TimeInSeconds\":[0.0,1.25],\"MsBetweenPresents\":[16.0,17.0]}}," +
                "{\"CaptureData\":{\"TimeInSeconds\":[1.25,2.75],\"MsBetweenPresents\":[18.0,19.0]}}]}");
            var fileInfo = new FileInfo(path);

            var lightweight = await _recordManager.GetFileRecordInfo(fileInfo);
            var cached = await _recordManager.GetFileRecordInfo(new FileInfo(path));
            var fullSession = _recordManager.LoadData(path);
            var full = FileRecordInfo.Create(fileInfo, fullSession);

            Assert.IsNotNull(lightweight);
            Assert.AreSame(lightweight, cached);
            Assert.AreEqual(full.Hash, lightweight.Hash);
            Assert.AreEqual(full.Id, lightweight.Id);
            Assert.AreEqual(full.ProcessName, lightweight.ProcessName);
            Assert.AreEqual(full.GameName, lightweight.GameName);
            Assert.AreEqual(full.ProcessorName, lightweight.ProcessorName);
            Assert.AreEqual(full.GraphicCardName, lightweight.GraphicCardName);
            Assert.AreEqual(full.SystemRamInfo, lightweight.SystemRamInfo);
            Assert.AreEqual(full.Comment, lightweight.Comment);
            Assert.AreEqual(full.IsAggregated, lightweight.IsAggregated);
            Assert.AreEqual(2.75, lightweight.RecordTime, 1E-12);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_ProcessListNameChanged_RefreshesMemoryCache()
        {
            var processList = (ProcessList)Activator.CreateInstance(typeof(ProcessList),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[]
                {
                    Path.Combine(_testDirectory, "processes.json"),
                    new Mock<IAppConfiguration>().Object,
                    new Mock<ILogger<ProcessList>>().Object
                }, null);
            processList.AddEntry("RenameMe.exe", "First Name");
            var manager = CreateRecordManager(processList);
            string path = CreateMetadataSessionFile("rename.json", "RenameMe.exe", "RenameMe.exe");

            var first = await manager.GetFileRecordInfo(new FileInfo(path));
            Assert.AreEqual("First Name", first.GameName);

            processList.FindProcessByName("RenameMe.exe").UpdateDisplayName("Second Name");
            var memoryCached = await manager.GetFileRecordInfo(new FileInfo(path));

            Assert.AreEqual("Second Name", memoryCached.GameName);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_IncompleteJsonMetadata_ReturnsNull()
        {
            string infoOnlyPath = Path.Combine(_testDirectory, "info-only.json");
            string runsOnlyPath = Path.Combine(_testDirectory, "runs-only.json");
            File.WriteAllText(infoOnlyPath,
                "{\"Hash\":\"hash\",\"Info\":{\"ProcessName\":\"MissingRuns.exe\"}}");
            File.WriteAllText(runsOnlyPath,
                "{\"Hash\":\"hash\",\"Runs\":[{\"CaptureData\":{\"TimeInSeconds\":[0,1]}}]}");

            var infoOnly = await _recordManager.GetFileRecordInfo(new FileInfo(infoOnlyPath));
            var runsOnly = await _recordManager.GetFileRecordInfo(new FileInfo(runsOnlyPath));

            Assert.IsNull(infoOnly);
            Assert.IsNull(runsOnly);
        }

        [TestMethod]
        public void ConvertPresentData_QuotedCsvFields_FilterAndParseCorrectly()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null,
                new EventAggregator(), new MockCaptureService(), null);
            const string header = "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange";
            var selectedRows = new[]
            {
                "\"P,One\",1,\"A,1\",\"10.00\",\"20\",\"21\"",
                "\"P,One\",1,\"A,1\",\"10.02\",\"22\",\"23\""
            };
            var lines = new List<string>
            {
                header,
                "\"Other\",2,\"B\",1.0,11,12"
            };
            lines.AddRange(selectedRows);

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            Assert.AreEqual(string.Join(",", selectedRows).GetSha1(), run.Hash);
            CollectionAssert.AreEqual(new[] { 20d, 22d }, run.CaptureData.MsBetweenPresents);
            CollectionAssert.AreEqual(new[] { 21d, 23d }, run.CaptureData.MsBetweenDisplayChange);
            Assert.AreEqual(0d, run.CaptureData.TimeInSeconds[0], 1E-12);
            Assert.AreEqual(0.02d, run.CaptureData.TimeInSeconds[1], 1E-12);
        }

        [TestMethod]
        public void ConvertPresentData_DisplayMetadata_KeepsLayersOfIdentifiedPresents()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null,
                new EventAggregator(), new MockCaptureService(), null);
            // PresentMon writes LayerIndex 0 and PresentId 0 for presents without display layer data.
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,VidPnSourceId,LayerIndex,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange,PresentId",
                "Game,1,A,0,0,1.00,10,10,0",
                "Game,1,A,1,1,1.01,10,10,4711",
                "Game,1,A,1,0,1.02,10,10,4712"
            };

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            CollectionAssert.AreEqual(new[] { -1, 1, 0 }, run.CaptureData.LayerIndex);
        }

        [TestMethod]
        public void ConvertPresentData_DisplayMetadataWithoutLayers_StoresNoLayerIndex()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null,
                new EventAggregator(), new MockCaptureService(), null);
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,VidPnSourceId,LayerIndex,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange,PresentId",
                "Game,1,A,0,0,1.00,10,10,0",
                "Game,1,A,0,0,1.01,10,10,0"
            };

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            Assert.AreEqual(0, run.CaptureData.LayerIndex.Length);
        }

        [TestMethod]
        public void ConvertPresentData_WithoutDisplayMetadata_StoresNoLayerIndex()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null,
                new EventAggregator(), new MockCaptureService(), null);
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange",
                "Game,1,A,1.00,10,10",
                "Game,1,A,1.01,10,10"
            };

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            Assert.AreEqual(0, run.CaptureData.LayerIndex.Length);
        }

        // --- FrameView sensor import ---
        //
        // FrameView logs sensors once per frame. The import resamples them onto 250 ms cells that start
        // at the first frame, with each measure time in the middle of its cell, fills
        // BetweenMeasureTime (required for run aggregation), maps the GPU power columns, picks the GPU
        // the analysis shows and flips the sign of the battery discharge rate. The end-to-end tests
        // with a complete FrameView log are in Integration/FrameViewSensorImportTest.

        private const double SensorInterval = RecordManager.EmbeddedSensorInterval;

        [TestMethod]
        public void ConvertPresentData_FrameViewSensors_ResampleTo250msGridAndFillBetweenMeasureTime()
        {
            var lines = BuildFrameViewLines(frameCount: 40);

            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
            var sensorData = run.SensorData2;

            // 40 frames span 0.651 s: three cells, measured in their middle.
            CollectionAssert.AreEqual(new[] { 0.125, 0.375, 0.625 }, sensorData.MeasureTime.Values.ToArray());

            // BetweenMeasureTime is filled: the first interval is the gap to the first frame, then
            // 250 ms. Without it run aggregation ends up with an empty sensor report.
            CollectionAssert.AreEqual(new[] { 0.125, 0.25, 0.25 }, sensorData.BetweenMeasureTime.Values.ToArray());

            // AMD board power maps to the GPU Power metric.
            Assert.IsTrue(sensorData.ContainsKey("amdpwr(w)(api)"));
            var amdPower = sensorData["amdpwr(w)(api)"].Values.ToArray();
            Assert.IsTrue(amdPower.All(v => !double.IsNaN(v)));
            Assert.AreEqual(200.0, amdPower.Average(), 1E-6);
            Assert.AreEqual(200, sensorData.GpuPower.Average(), 1E-6);

            // Battery discharge rate is flipped to a positive value (FrameView reports it negative)
            // and keeps the "Discharge Rate" name of the native sensor.
            Assert.IsTrue(sensorData.ContainsKey("batterydrainrate(w)"));
            Assert.AreEqual("Discharge Rate", sensorData["batterydrainrate(w)"].Name);
            var drain = sensorData["batterydrainrate(w)"].Values.ToArray();
            Assert.IsTrue(drain.All(v => v > 0),
                "battery drain rate must be positive after the sign flip");

            // A column that is entirely NA is dropped, not stored as a zero series.
            Assert.IsFalse(sensorData.ContainsKey("gpu1clk(mhz)"),
                "an all-NA sensor column must be skipped, not added as a zero series");
        }

        [TestMethod]
        public void ConvertPresentData_FrameViewSensors_GapWithoutSamplesStaysNaN()
        {
            // A sensor dropout inside the log must stay a hole. The resampler interpolates across
            // a gap and extends the outer samples, so without the mask the middle cell would report
            // a value between the readings around the gap that was never measured.
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,GPU0Clk (MHz)"
            };
            for (int i = 0; i < 40; i++)
            {
                // Frames 15..29 (0.2505 s .. 0.4843 s) leave the second cell (0.25 s .. 0.5 s) empty.
                string clock = i >= 15 && i <= 29 ? "NA" : i < 15 ? "1000" : "2000";
                lines.Add(FormattableString.Invariant($"Game,1,A,{i * 0.0167:0.000},16.7,{clock}"));
            }

            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
            var clockValues = run.SensorData2["gpu0clk(mhz)"].Values.ToArray();

            Assert.AreEqual(3, clockValues.Length);
            Assert.AreEqual(1000.0, clockValues[0], 1E-6);
            Assert.IsTrue(double.IsNaN(clockValues[1]),
                "a cell without a single sample must stay NaN instead of being interpolated");
            Assert.AreEqual(2000.0, clockValues[2], 1E-6);
        }

        [TestMethod]
        public void ConvertPresentData_FrameViewSensors_NaNExactlyWhereACellHoldsNoSample()
        {
            // The mask and the resampler's cells must be the same spans: a cell is empty exactly when
            // no frame with a value falls into the 250 ms around its measure time. A mask half a cell
            // off blanked cells that held real samples and let interpolated values through elsewhere.
            var random = new Random(480);
            for (int pattern = 0; pattern < 25; pattern++)
            {
                const int frameCount = 600;
                double frameTime = 0.004 + random.NextDouble() * 0.03;
                var hasValue = new bool[frameCount];
                bool inGap = false;
                for (int i = 0; i < frameCount; i++)
                {
                    // Dropouts of random length, from single frames to several cells.
                    if (random.NextDouble() < 0.02)
                        inGap = !inGap;
                    hasValue[i] = !inGap;
                }

                var lines = new List<string>
                {
                    "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,GPU0Temp (C)"
                };
                for (int i = 0; i < frameCount; i++)
                {
                    lines.Add(FormattableString.Invariant(
                        $"Game,1,A,{i * frameTime:0.000000},{frameTime * 1000:0.000},{(hasValue[i] ? (40 + i % 7).ToString() : "NA")}"));
                }

                var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
                if (!run.SensorData2.ContainsKey("gpu0temp(c)"))
                {
                    Assert.IsFalse(hasValue.Any(value => value), $"pattern {pattern}: the sensor was dropped");
                    continue;
                }

                var measureTimes = run.SensorData2.MeasureTime.Values.ToArray();
                var temperatures = run.SensorData2["gpu0temp(c)"].Values.ToArray();
                var frameTimes = run.CaptureData.TimeInSeconds;
                for (int cell = 0; cell < measureTimes.Length; cell++)
                {
                    double cellStart = measureTimes[cell] - SensorInterval / 2;
                    double cellEnd = measureTimes[cell] + SensorInterval / 2;
                    bool cellHasSample = Enumerable.Range(0, frameCount).Any(i => hasValue[i]
                        && frameTimes[i] >= cellStart && frameTimes[i] < cellEnd);

                    Assert.AreEqual(cellHasSample, !double.IsNaN(temperatures[cell]),
                        $"pattern {pattern}, cell {cell} at {measureTimes[cell]:0.000} s");
                    if (cellHasSample)
                        Assert.IsTrue(temperatures[cell] >= 40 - 1E-6 && temperatures[cell] <= 46 + 1E-6,
                            $"pattern {pattern}, cell {cell}: {temperatures[cell]} is outside the logged range");
                }
            }
        }

        [TestMethod]
        public void ConvertPresentData_FrameViewSensors_AggregatesTwoRunsViaBetweenMeasureTime()
        {
            var run1 = _recordManager.ConvertPresentDataLinesToSessionRun(BuildFrameViewLines(frameCount: 40));
            var run2 = _recordManager.ConvertPresentDataLinesToSessionRun(BuildFrameViewLines(frameCount: 40));

            // Aggregating two runs rebuilds MeasureTime from BetweenMeasureTime: the path that
            // failed before BetweenMeasureTime was filled on import.
            _recordManager.NormalizeStartTimesOfSessionRuns(new List<ISessionRun> { run1, run2 });

            var run1Measure = run1.SensorData2.MeasureTime.Values.ToArray();
            var run2Measure = run2.SensorData2.MeasureTime.Values.ToArray();

            CollectionAssert.AreEqual(new[] { 0.125, 0.375, 0.625 }, run1Measure);
            // The second run continues half an interval after the last cell of the first one, so no
            // measure time repeats at the seam.
            CollectionAssert.AreEqual(new[] { 0.75, 1.0, 1.25 }, run2Measure);
            // Its first cell stays within half an interval of where it belongs: the middle of the
            // first 250 ms after its first frame (0.668 s).
            Assert.AreEqual(run2.CaptureData.TimeInSeconds[0] + SensorInterval / 2, run2Measure[0], SensorInterval / 2);
        }

        [TestMethod]
        [DataRow("PCAT Power Total(W),NV Pwr(W) (API)", "300.0,250.0", DisplayName = "PCAT ahead of the API column")]
        [DataRow("NV Pwr(W) (API),PCAT Power Total(W)", "250.0,300.0", DisplayName = "PCAT behind the API column")]
        public void ConvertPresentData_PcatPower_WinsOverApiGpuPower(string powerHeaders, string powerValues)
        {
            // PCAT is an actual measurement, so it must feed the "GPU Power" metric even when the
            // driver also reports board power, whatever the column order of the FrameView version.
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents," + powerHeaders
            };
            for (int i = 0; i < 40; i++)
                lines.Add(FormattableString.Invariant($"Game,1,A,{i * 0.0167:0.000},16.7,{powerValues}"));

            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
            var sensorData = run.SensorData2;

            var power = sensorData.Values
                .FirstOrDefault(c => c.Name.Contains("GPU Power") && c.Type == "Power");
            Assert.IsNotNull(power);
            Assert.AreEqual("GPU Power (PCAT)", power.Name);

            var gpuPower = sensorData.GpuPower;
            Assert.IsTrue(gpuPower.Length > 0);
            Assert.AreEqual(300, gpuPower.Average(), 1E-6,
                "GPU Power must come from PCAT, not from the NV Pwr API value");
            Assert.AreEqual(250, sensorData["nvpwr(w)(api)"].Values.Average(), 1E-6,
                "the API value stays available in the sensor table");
        }

        [TestMethod]
        public void ConvertPresentData_TwoGpus_TheAnalysisShowsTheBusyOne()
        {
            // A laptop log: GPU0 is the idle iGPU, GPU1 the GPU that rendered the game.
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents," +
                "GPU0Clk(MHz),GPU0Util(%),GPU0Temp(C),GPU1Clk(MHz),GPU1Util(%),GPU1Temp(C),NV Pwr(W) (API)"
            };
            for (int i = 0; i < 40; i++)
                lines.Add(FormattableString.Invariant($"Game,1,A,{i * 0.0167:0.000},16.7,600,3,45,2100,97,70,95.0"));

            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
            var sensorData = run.SensorData2;

            Assert.AreEqual(97, sensorData.GpuUsage.Average(), 1E-6);
            Assert.AreEqual(2100, sensorData.GpuClock.Average(), 1E-6);
            Assert.AreEqual(70, sensorData.GpuTemp.Average(), 1E-6);
            Assert.AreEqual("GPU Core", sensorData["gpu1util(%)"].Name);
            // The iGPU stays in the sensor table under names the analysis does not read.
            Assert.AreEqual("GPU0 Core", sensorData["gpu0util(%)"].Name);
            Assert.AreEqual("GPU0 Core", sensorData["gpu0clk(mhz)"].Name);
            Assert.AreEqual(3, sensorData["gpu0util(%)"].Values.Average(), 1E-6);
        }

        [TestMethod]
        public void ConvertPresentData_OnlyGpu1Reports_ItIsTheGpuCore()
        {
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents," +
                "GPU0Clk(MHz),GPU0Util(%),GPU1Clk(MHz),GPU1Util(%)"
            };
            for (int i = 0; i < 40; i++)
                lines.Add(FormattableString.Invariant($"Game,1,A,{i * 0.0167:0.000},16.7,NA,NA,1800,88"));

            var sensorData = _recordManager.ConvertPresentDataLinesToSessionRun(lines).SensorData2;

            Assert.IsFalse(sensorData.ContainsKey("gpu0util(%)"));
            Assert.AreEqual("GPU Core", sensorData["gpu1util(%)"].Name);
            Assert.AreEqual(88, sensorData.GpuUsage.Average(), 1E-6);
            Assert.AreEqual(1800, sensorData.GpuClock.Average(), 1E-6);
        }

        [TestMethod]
        public void ConvertPresentData_CsvWithoutSensorColumns_KeepsThePerFrameMeasureTime()
        {
            // PresentMon files and every live capture (CaptureManager converts its rows the same way
            // and then replaces the sensor data) take the path they took before the FrameView import.
            var lines = new List<string> { "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents" };
            for (int i = 0; i < 40; i++)
                lines.Add(FormattableString.Invariant($"Game,1,A,{i * 0.0167:0.000},16.7"));

            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);

            CollectionAssert.AreEqual(run.CaptureData.TimeInSeconds, run.SensorData2.MeasureTime.Values.ToArray());
            Assert.AreEqual(0, run.SensorData2.BetweenMeasureTime.Values.Count);
            Assert.AreEqual(2, run.SensorData2.Count, "only MeasureTime and BetweenMeasureTime");
        }

        // End-to-end check against a real, trimmed FrameView 2.0 capture (AMD Ryzen 7 9850X3D +
        // RTX 5060 Ti, an NVIDIA-based system), kept at 150 frames. This exercises the genuine
        // header layout ("GPU0Clk (MHz)", "NV Pwr(W) (API)", "CPU Package Power(W)") and genuine NA
        // cells - things the inline synthetic cases above cannot guarantee.
        [TestMethod]
        public void ConvertPresentData_AmdRyzenFixture_ResamplesRealFormatAndMapsGpuPower()
        {
            var lines = new List<string>(
                File.ReadAllLines(@"TestRecordFiles\FrameView_AmdRyzen9850X3D_Trimmed.csv"));

            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
            var sensorData = run.SensorData2;

            // MeasureTime sits on a uniform 250 ms grid, measured in the middle of each cell.
            var measureTimes = sensorData.MeasureTime.Values.ToArray();
            Assert.IsTrue(measureTimes.Length >= 2, "expected a 250 ms grid with at least two points");
            Assert.AreEqual(SensorInterval / 2, measureTimes[0], 1E-9);
            for (int i = 1; i < measureTimes.Length; i++)
                Assert.AreEqual(SensorInterval, measureTimes[i] - measureTimes[i - 1], 1E-6,
                    $"MeasureTime step at index {i} should be 250 ms");

            // BetweenMeasureTime is filled (the gap to the first frame, then 250 ms each) so run
            // aggregation no longer produces an empty sensor report.
            var between = sensorData.BetweenMeasureTime.Values.ToArray();
            Assert.AreEqual(measureTimes.Length, between.Length);
            Assert.AreEqual(SensorInterval / 2, between[0], 1E-9);
            for (int i = 1; i < between.Length; i++)
                Assert.AreEqual(SensorInterval, between[i], 1E-6);

            // On this NVIDIA system the GPU power is reported via "NV Pwr(W) (API)" and must map
            // to the "GPU Power" metric (real per-frame values, not synthetic). The "AMDPwr(W) (API)"
            // column is present in the header but all-NA, so it is dropped rather than stored.
            Assert.IsTrue(sensorData.ContainsKey("nvpwr(w)(api)"), "NV board power must map to GPU Power");
            var nvPower = sensorData["nvpwr(w)(api)"].Values.ToArray();
            Assert.IsTrue(nvPower.Length > 0);
            Assert.IsTrue(nvPower.All(v => !double.IsNaN(v)), "NV power must not be NaN after resampling");

            // The all-NA AMDPwr column must be skipped, not added as a zero series.
            Assert.IsFalse(sensorData.ContainsKey("amdpwr(w)(api)"),
                "an all-NA sensor column must be skipped, not added as a zero series");

            // x86: CPU Package Power is the CPU power source.
            Assert.IsTrue(sensorData.ContainsKey("cpupackagepower(w)"));

            // GPU1 columns are all-NA in this single-GPU capture, so they must be dropped,
            // not stored as a zero series.
            Assert.IsFalse(sensorData.ContainsKey("gpu1clk(mhz)"),
                "an all-NA sensor column must be skipped, not added as a zero series");
        }

        // Real RTX SPARK (N1X) capture that begins with a 16-line "//" comment block (added by
        // external tooling, not FrameView). The skip mirrors LoadSessionFromCSV's comment handling
        // before the converter runs. The laptop runs on battery: the drain rate is NA for the first
        // rows and -12.677 W afterwards.
        [TestMethod]
        public void ConvertPresentData_RtxSparkFixture_SkipsCommentBlock()
        {
            var rawLines = File.ReadAllLines(@"TestRecordFiles\FrameView_RtxSparkN1X_Trimmed.csv");
            Assert.IsTrue(rawLines[0].StartsWith(FileRecordInfo.HEADER_MARKER),
                "fixture must begin with a comment block to exercise the skip path");

            var lines = rawLines
                .SkipWhile(line => line.Contains(FileRecordInfo.HEADER_MARKER))
                .ToList();
            var run = _recordManager.ConvertPresentDataLinesToSessionRun(lines);
            Assert.IsNotNull(run);
            var sensorData = run.SensorData2;

            // Comment block skipped: sensors are parsed from the real header that follows it.
            Assert.IsTrue(sensorData.ContainsKey("gpu0clk(mhz)"),
                "GPU0 clock must be parsed after skipping the comment block");
            Assert.IsTrue(sensorData.MeasureTime.Values.Count >= 1,
                "MeasureTime must be populated from the data rows");

            // RTX SPARK keeps E/P-Core power as separate sensors (not merged), and without a package
            // power reading the analysis shows no CPU power.
            Assert.IsTrue(sensorData.ContainsKey("e-corepower(w)"));
            Assert.IsTrue(sensorData.ContainsKey("p-corepower(w)"));
            Assert.IsFalse(sensorData.ContainsKey("cpupackagepower(w)"));
            Assert.AreEqual(0, sensorData.CpuPower.Length);

            // Battery: the native names, the drain rate positive while discharging.
            Assert.AreEqual("Charge Level", sensorData["batterypercentage"].Name);
            Assert.AreEqual(99.192, sensorData["batterypercentage"].Values.Average(), 1E-3);
            var discharge = sensorData["batterydrainrate(w)"].Values.Where(double.IsFinite).ToArray();
            Assert.IsTrue(discharge.Length > 0);
            Assert.IsTrue(discharge.All(value => value > 0), "the discharge rate must be positive");
            Assert.AreEqual(12.677, discharge.Max(), 1E-3);
        }

        private static List<string> BuildFrameViewLines(int frameCount)
        {
            var lines = new List<string>
            {
                "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents," +
                "GPU0Clk (MHz),GPU0Util (%),AMDPwr (W) (API),Battery Drain Rate (W),GPU1Clk (MHz)"
            };

            for (int i = 0; i < frameCount; i++)
            {
                double t = i * 0.0167;
                // GPU1Clk is always NA -> exercises the all-NA column drop path.
                lines.Add(FormattableString.Invariant($"Game,1,A,{t:0.000},16.7,1500,50,200.0,{-(12.0 + i * 0.1):0.0},NA"));
            }

            return lines;
        }

        private string CreateSessionFile(string fileName, string processName)
        {
            string path = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(path, CreateSessionJson(processName));
            return path;
        }

        private static string CreateSessionJson(string processName)
        {
            return "{\"Hash\":\"hash\",\"Info\":{\"ProcessName\":\"" + processName + "\"},\"Runs\":[]}";
        }

        private string CreateMetadataSessionFile(string fileName, string processName, string gameName)
        {
            string path = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(path,
                "{\"Hash\":\"metadata-hash\",\"Info\":{" +
                "\"Id\":\"8f9f8c95-f1c1-4a38-90b4-61c74a40df35\"," +
                "\"CreationDate\":\"2026-07-18T12:00:00Z\"," +
                "\"ProcessName\":\"" + processName + "\"," +
                "\"GameName\":\"" + gameName + "\",\"Comment\":\"metadata\"}," +
                "\"Runs\":[{\"CaptureData\":{\"TimeInSeconds\":[0.0,2.5]}}]}");
            return path;
        }

        private int GetPrivateCacheCount(string fieldName)
        {
            var field = typeof(RecordManager).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var cache = field.GetValue(_recordManager) as System.Collections.IDictionary;
            Assert.IsNotNull(cache);
            return cache.Count;
        }

    }
}
