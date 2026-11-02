using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// End-to-end import of a complete FrameView 2.0 log, which repeats the last reading of every
    /// sensor in every frame row. Each derived class is one scenario: a log, the sensors it reports and
    /// the GPU that rendered the game. The record is loaded as the app loads it
    /// (<see cref="RecordManager.LoadData"/>), and its sensors are followed into the analysis, the
    /// sensor tab, FPS/W and a saved, aggregated and cut record. The expected values come from
    /// <see cref="FrameViewLog"/>, which reads the log on its own.
    /// </summary>
    public abstract class FrameViewSensorImportTest
    {
        protected const double Interval = RecordManager.EmbeddedSensorInterval;
        // The analysis rounds every cell to a whole number and the average again.
        protected const double RoundedTolerance = 1;
        private const string CpuPowerColumn = "CPU Package Power(W)";

        // The unit the sensor tab appends to an entry name, by sensor type.
        private static readonly Dictionary<string, string> SensorTabUnits = new Dictionary<string, string>
        {
            ["Clock"] = "MHz",
            ["Load"] = "%",
            ["Temperature"] = "°C",
            ["Power"] = "W",
        };

        private string _folder;

        /// <summary>The log, relative to the test output.</summary>
        protected abstract string LogFile { get; }

        protected abstract string ProcessName { get; }

        protected abstract string Gpu { get; }

        protected abstract string Processor { get; }

        protected abstract int FrameCount { get; }

        /// <summary>The cells of <see cref="Interval"/> the frames span.</summary>
        protected abstract int CellCount { get; }

        /// <summary>The sensor columns of the log that report values, by entry key: their column and the
        /// name and type of the entry.</summary>
        protected abstract IReadOnlyDictionary<string, (string Column, string Name, string Type)> LoggedSensors { get; }

        /// <summary>The index of the GPU that rendered the game, whose GPU#... columns the analysis reads.</summary>
        protected abstract int PrimaryGpu { get; }

        /// <summary>The column the analysis takes the GPU power from.</summary>
        protected abstract string GpuPowerColumn { get; }

        protected FrameViewLog Log { get; private set; }

        private string GpuPowerKey => LoggedSensors.Single(sensor => sensor.Value.Column == GpuPowerColumn).Key;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.FrameViewSensorImportTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            Log = FrameViewLog.Read(LogFile);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void LoadedRecord_DescribesTheLog()
        {
            var session = Manager().LoadData(LogFile);

            Assert.IsNotNull(session, "the FrameView log did not load");
            Assert.AreEqual(ProcessName, session.Info.ProcessName);
            Assert.AreEqual(Gpu, session.Info.GPU);
            Assert.AreEqual(Processor, session.Info.Processor);
            Assert.AreEqual(1, session.Runs.Count);
            Assert.AreEqual(FrameCount, Log.FrameCount);
            CollectionAssert.AreEqual(Log.Column("MsBetweenPresents"), session.Runs[0].CaptureData.MsBetweenPresents);
            CollectionAssert.AreEqual(Log.Times, session.Runs[0].CaptureData.TimeInSeconds);
        }

        [TestMethod]
        public void Sensors_AreTheColumnsThatReportValues()
        {
            var sensorData = Load().SensorData2;

            CollectionAssert.AreEquivalent(
                LoggedSensors.Keys.Append("MeasureTime").Append("BetweenMeasureTime").ToArray(),
                sensorData.Keys.ToArray());
            foreach (var sensor in LoggedSensors)
            {
                Assert.AreEqual(sensor.Value.Name, sensorData[sensor.Key].Name, sensor.Key);
                Assert.AreEqual(sensor.Value.Type, sensorData[sensor.Key].Type, sensor.Key);
            }
        }

        [TestMethod]
        public void Sensors_SitOnTheNativeSensorInterval()
        {
            var sensorData = Load().SensorData2;
            int cellCount = (int)Math.Floor(Log.Duration / Interval) + 1;

            // Cells of 250 ms from the first frame, each measured in its middle: four values per second
            // like a native capture, instead of one per frame.
            Assert.AreEqual(CellCount, cellCount);
            var measureTimes = sensorData.MeasureTime.Values.ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(0, cellCount).Select(b => (b + 0.5) * Interval).ToArray(), measureTimes);
            CollectionAssert.AreEqual(
                Enumerable.Range(0, cellCount).Select(b => b == 0 ? Interval / 2 : Interval).ToArray(),
                sensorData.BetweenMeasureTime.Values.ToArray());

            foreach (var key in LoggedSensors.Keys)
            {
                var values = sensorData[key].Values.ToArray();
                Assert.AreEqual(cellCount, values.Length, key);
                Assert.IsTrue(values.All(double.IsFinite), $"{key}: the log has a reading in every cell");
            }
        }

        [TestMethod]
        public void SensorValues_AreTheTimeWeightedMeansOfTheirCells()
        {
            var sensorData = Load().SensorData2;
            var measureTimes = sensorData.MeasureTime.Values.ToArray();

            foreach (var sensor in LoggedSensors)
            {
                var logged = Log.Column(sensor.Value.Column);
                var values = sensorData[sensor.Key].Values.ToArray();

                for (int cell = 0; cell < values.Length; cell++)
                {
                    double expected = Log.CellMean(logged, measureTimes[cell] - Interval / 2, measureTimes[cell] + Interval / 2);
                    Assert.AreEqual(expected, values[cell], 1E-5 * Math.Max(1, Math.Abs(expected)),
                        $"{sensor.Key}, cell {cell}");
                }

                // Weighted by time, not by frames: the cells keep the mean of the log...
                double logMean = Log.HoldMean(logged, 0, Log.Duration);
                Assert.AreEqual(logMean, values.Average(), 5E-4 * logMean, $"{sensor.Key}: mean");
                // ...and stay within its readings.
                Assert.IsTrue(values.Min() >= logged.Min() - 1E-6 && values.Max() <= logged.Max() + 1E-6,
                    $"{sensor.Key}: {values.Min()}..{values.Max()} is outside the logged {logged.Min()}..{logged.Max()}");
            }
        }

        [TestMethod]
        public void Analysis_ShowsTheLoggedSensors()
        {
            var report = SensorReport.GetReportFromSessionSensorData(new[] { Load().SensorData2 })
                .ToDictionary(item => item.Name, item => item.AverageValue);

            // The analysis rounds every average to a whole number.
            AssertReported(report, "CPU load (%)", "CPUUtil(%)");
            AssertReported(report, "CPU power (W)", CpuPowerColumn);
            AssertReported(report, "CPU temp (°C)", "CPU Package Temp(C)");
            AssertReported(report, "GPU load (%)", GpuColumn("Util(%)"));
            AssertReported(report, "GPU clock (MHz)", GpuColumn("Clk(MHz)"));
            AssertReported(report, "GPU power (W)", GpuPowerColumn);
            AssertReported(report, "GPU temp. (°C)", GpuColumn("Temp(C)"));

            // FrameView's CPU clock is the average, so the analysis shows no max clock; FrameView logs
            // no per-thread maximum, no TBP simulation, no VRAM and no RAM usage.
            CollectionAssert.AreEquivalent(new[]
            {
                "CPU load (%)", "CPU power (W)", "CPU temp (°C)", "GPU load (%)", "Time in GPU load limit (%)",
                "GPU clock (MHz)", "GPU power (W)", "GPU temp. (°C)"
            }, report.Keys.ToArray());
        }

        [TestMethod]
        public void Analysis_FpsPerWattUsesTheGpuAndThePackagePower()
        {
            var run = Load();
            var sensorData = new[] { run.SensorData2 };
            var provider = new FrametimeStatisticProvider(new Mock<IFrametimeStatisticProviderOptions>().Object);
            var frametimes = run.CaptureData.MsBetweenPresents.ToList();
            double fps = Log.FrameCount * 1000 / Log.Column("MsBetweenPresents").Sum();

            double gpuPower = SensorReport.GetAverageSensorValues(sensorData, EReportSensorName.GpuPower);
            double cpuPower = SensorReport.GetAverageSensorValues(sensorData, EReportSensorName.CpuPower);

            Assert.AreEqual(Log.HoldMean(Log.Column(GpuPowerColumn), 0, Log.Duration), gpuPower, RoundedTolerance);
            Assert.AreEqual(Log.HoldMean(Log.Column(CpuPowerColumn), 0, Log.Duration), cpuPower, RoundedTolerance);
            Assert.AreEqual(fps / gpuPower * 10,
                provider.GetPhysicalMetricValue(frametimes, EMetric.GpuFpsPerWatt, gpuPower), 0.01);
            Assert.AreEqual(fps / cpuPower * 10,
                provider.GetPhysicalMetricValue(frametimes, EMetric.CpuFpsPerWatt, cpuPower), 0.01);
        }

        [TestMethod]
        public void SensorTab_ListsEveryImportedSensor()
        {
            var items = SensorReport.GetFullReportFromSessionSensorData(new[] { Load().SensorData2 })
                .ToDictionary(item => item.Name);

            // Every entry under its name and unit, and the GPU limit time.
            var expected = LoggedSensors.Values.ToDictionary(
                sensor => $"{sensor.Name} ({SensorTabUnits[sensor.Type]})", sensor => sensor.Column);
            CollectionAssert.AreEquivalent(expected.Keys.Append("GPU Limit Time (%)").ToArray(), items.Keys.ToArray());

            foreach (var row in expected)
            {
                var item = items[row.Key];
                double logMean = Log.HoldMean(Log.Column(row.Value), 0, Log.Duration);
                // Rounded to the digits of the sensor type, plus the cells' deviation from the log.
                double tolerance = 0.5 * Math.Pow(10, -item.RoundingDigits) + 5E-4 * logMean;
                Assert.AreEqual(logMean, item.AverageValue, tolerance, row.Key);
            }
        }

        [TestMethod]
        public async Task SavedRecord_KeepsTheSensorData()
        {
            var manager = Manager();
            var session = manager.LoadData(LogFile);

            var saved = await SaveAndLoad(manager, session.Runs, session.Info);

            Assert.AreEqual(1, saved.Runs.Count);
            AssertSameSensorData(session.Runs[0].SensorData2, saved.Runs[0].SensorData2);
            CollectionAssert.AreEqual(
                SensorReport.GetReportFromSessionSensorData(new[] { session.Runs[0].SensorData2 }).Select(item => item.AverageValue).ToArray(),
                SensorReport.GetReportFromSessionSensorData(new[] { saved.Runs[0].SensorData2 }).Select(item => item.AverageValue).ToArray());
        }

        [TestMethod]
        public async Task AggregatedRecord_ContinuesTheSensorTimelineAcrossRuns()
        {
            // Three consecutive parts of the log, loaded as three FrameView records, aggregated into
            // one record and read back.
            var manager = Manager();
            var parts = Enumerable.Range(0, 3).Select(part => manager.LoadData(WritePart(part, 3))).ToArray();
            var partSensorData = parts.Select(part => part.Runs[0].SensorData2).ToArray();

            var aggregated = await SaveAndLoad(manager, parts.Select(part => part.Runs[0]), parts[0].Info);

            Assert.AreEqual(3, aggregated.Runs.Count);
            double longestFrame = Log.Column("MsBetweenPresents").Max() / 1000;
            var allMeasureTimes = new List<double>();
            for (int run = 0; run < 3; run++)
            {
                var sensorData = aggregated.Runs[run].SensorData2;
                var measureTimes = sensorData.MeasureTime.Values.ToArray();
                var frameTimes = aggregated.Runs[run].CaptureData.TimeInSeconds;

                // Each run keeps its own values; only the measure times move onto the shared timeline.
                foreach (var key in LoggedSensors.Keys)
                    CollectionAssert.AreEqual(partSensorData[run][key].Values.ToArray(), sensorData[key].Values.ToArray(), $"run {run}, {key}");
                Assert.AreEqual(measureTimes.Length, sensorData.BetweenMeasureTime.Values.Count);
                for (int i = 1; i < measureTimes.Length; i++)
                    Assert.AreEqual(Interval, measureTimes[i] - measureTimes[i - 1], 1E-9, $"run {run}, measure time {i}");

                // A run continues half an interval after the last cell of the run before: no measure
                // time repeats at a seam.
                if (run > 0)
                    Assert.AreEqual(Interval / 2, measureTimes[0] - allMeasureTimes.Last(), 1E-9, $"run {run}: seam");

                // The sensor timeline follows the frames. The aggregation chains the runs by their
                // measure intervals, as for native captures, so each seam may move the next run by up to
                // half an interval and a frame against its frames, in either direction; before, every
                // seam moved it up to an interval and a frame earlier.
                Assert.AreEqual(frameTimes[0] + Interval / 2, measureTimes[0], run * (Interval / 2 + longestFrame) + 1E-9,
                    $"run {run}: first cell");
                allMeasureTimes.AddRange(measureTimes);
            }

            // The aggregated record has a sensor report, weighted by time like a native one.
            var report = SensorReport.GetReportFromSessionSensorData(aggregated.Runs.Select(run => run.SensorData2))
                .ToDictionary(item => item.Name, item => item.AverageValue);
            AssertReported(report, "GPU power (W)", GpuPowerColumn);
            AssertReported(report, "CPU power (W)", CpuPowerColumn);
            AssertReported(report, "GPU load (%)", GpuColumn("Util(%)"));
            Assert.IsTrue(SensorReport.GetFullReportFromSessionSensorData(aggregated.Runs.Select(run => run.SensorData2))
                .Any(item => item.Name == "GPU Power (W)"));
        }

        [TestMethod]
        public async Task CutRecord_KeepsTheSensorsOfTheRange()
        {
            var manager = Manager();
            var session = manager.LoadData(LogFile);

            await manager.DuplicateSession(session, inverse: false, startTime: 5, endTime: 15, _folder);
            var cut = manager.LoadData(Directory.GetFiles(_folder, "*.json").Single());

            // Cells 20 to 59, measured at 5.125 to 14.875 s.
            var source = session.Runs[0].SensorData2;
            var sensorData = cut.Runs[0].SensorData2;
            foreach (var key in LoggedSensors.Keys)
                CollectionAssert.AreEqual(source[key].Values.Skip(20).Take(40).ToArray(), sensorData[key].Values.ToArray(), key);

            // The cut starts at its first frame, so does its sensor timeline.
            double firstKeptFrame = Log.Times.First(time => time >= 5);
            var measureTimes = sensorData.MeasureTime.Values.ToArray();
            Assert.AreEqual(5.125 - firstKeptFrame, measureTimes[0], 1E-9);
            for (int i = 1; i < measureTimes.Length; i++)
                Assert.AreEqual(Interval, measureTimes[i] - measureTimes[i - 1], 1E-9, $"measure time {i}");

            var report = SensorReport.GetReportFromSessionSensorData(new[] { sensorData })
                .ToDictionary(item => item.Name, item => item.AverageValue);
            Assert.AreEqual(Log.HoldMean(Log.Column(GpuPowerColumn), 5, 15), report["GPU power (W)"], RoundedTolerance);
            Assert.AreEqual(Log.HoldMean(Log.Column(CpuPowerColumn), 5, 15), report["CPU power (W)"], RoundedTolerance);
        }

        [TestMethod]
        public void SensorDropout_StaysAGapInTheSensorTableAndIsBridgedInTheAnalysis()
        {
            // The GPU power drops out from 8.0 to 9.0 s, cells 32 to 35.
            var full = Load().SensorData2;
            int gpuPowerColumn = Log.Index(GpuPowerColumn);
            var path = WriteLog("dropout.csv", (row, time) =>
            {
                if (time >= 8.0 && time < 9.0)
                    row[gpuPowerColumn] = "NA";
            });

            var sensorData = Manager().LoadData(path).Runs[0].SensorData2;

            var power = sensorData[GpuPowerKey].Values.ToArray();
            var fullPower = full[GpuPowerKey].Values.ToArray();
            for (int cell = 0; cell < power.Length; cell++)
            {
                if (cell >= 32 && cell <= 35)
                    Assert.IsTrue(double.IsNaN(power[cell]), $"cell {cell} lies in the dropout");
                else if (cell == 31 || cell == 36)
                    Assert.IsTrue(double.IsFinite(power[cell]), $"cell {cell} holds readings next to the dropout");
                else
                    Assert.AreEqual(fullPower[cell], power[cell], $"cell {cell} is not affected");
            }

            // The other sensors are untouched.
            foreach (var key in LoggedSensors.Keys.Where(key => key != GpuPowerKey))
                CollectionAssert.AreEqual(full[key].Values.ToArray(), sensorData[key].Values.ToArray(), key);

            // The sensor tab averages the readings only...
            var item = SensorReport.GetFullReportFromSessionSensorData(new[] { sensorData }).Single(row => row.Name == "GPU Power (W)");
            Assert.AreEqual(Math.Round(power.Where(double.IsFinite).Average(), 1, MidpointRounding.AwayFromZero), item.AverageValue);
            // ...and the analysis carries the last reading through the gap, as for a native capture.
            var gpuPower = ((ISessionSensorData)sensorData).GpuPower;
            Assert.AreEqual(power.Length, gpuPower.Length);
            for (int cell = 32; cell <= 35; cell++)
                Assert.AreEqual(gpuPower[31], gpuPower[cell], $"cell {cell}");
        }

        [TestMethod]
        public void SameLogWithoutSensorColumns_LoadsTheSameFrames()
        {
            // The PresentMon columns of the log alone (everything before GPU0Clk): the sensor import
            // must not change the frames, and a file without sensor columns takes the former path.
            int firstSensorColumn = Log.Index("GPU0Clk(MHz)");
            var path = WriteLog("frames-only.csv", (row, time) => { }, columns: firstSensorColumn);

            var framesOnly = Manager().LoadData(path).Runs[0];
            var full = Load();

            CollectionAssert.AreEqual(full.CaptureData.TimeInSeconds, framesOnly.CaptureData.TimeInSeconds);
            CollectionAssert.AreEqual(full.CaptureData.MsBetweenPresents, framesOnly.CaptureData.MsBetweenPresents);
            CollectionAssert.AreEqual(full.CaptureData.MsBetweenDisplayChange, framesOnly.CaptureData.MsBetweenDisplayChange);
            CollectionAssert.AreEqual(full.CaptureData.MsUntilDisplayed, framesOnly.CaptureData.MsUntilDisplayed);
            CollectionAssert.AreEqual(full.CaptureData.PcLatency, framesOnly.CaptureData.PcLatency);
            CollectionAssert.AreEqual(full.CaptureData.Dropped, framesOnly.CaptureData.Dropped);

            CollectionAssert.AreEqual(framesOnly.CaptureData.TimeInSeconds, framesOnly.SensorData2.MeasureTime.Values.ToArray());
            Assert.AreEqual(0, framesOnly.SensorData2.BetweenMeasureTime.Values.Count);
            Assert.AreEqual(2, framesOnly.SensorData2.Count);
        }

        protected static RecordManager Manager()
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null);

        protected ISessionRun Load()
            => Manager().LoadData(LogFile).Runs[0];

        // The column of the primary GPU, e.g. "Util(%)" for "GPU1Util(%)".
        protected string GpuColumn(string column) => $"GPU{PrimaryGpu}{column}";

        private async Task<ISession> SaveAndLoad(RecordManager manager, IEnumerable<ISessionRun> runs, ISessionInfo info)
        {
            foreach (var file in Directory.GetFiles(_folder, "*.json"))
                File.Delete(file);

            Assert.IsTrue(await manager.SaveSessionRunsToFile(runs, info.ProcessName, "FrameView import", _folder,
                new List<ISessionInfo> { info }), "the record was not written");
            return manager.LoadData(Directory.GetFiles(_folder, "*.json").Single());
        }

        private static void AssertSameSensorData(ISessionSensorData2 expected, ISessionSensorData2 actual)
        {
            CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
            foreach (var entry in expected)
            {
                Assert.AreEqual(entry.Value.Name, actual[entry.Key].Name, entry.Key);
                Assert.AreEqual(entry.Value.Type, actual[entry.Key].Type, entry.Key);
                CollectionAssert.AreEqual(entry.Value.Values.ToArray(), actual[entry.Key].Values.ToArray(), entry.Key);
            }
        }

        // Asserts an analysis average against the time-weighted mean of the log column.
        private void AssertReported(Dictionary<string, double> report, string item, string column)
        {
            Assert.IsTrue(report.ContainsKey(item), $"the analysis shows no {item}");
            Assert.AreEqual(Log.HoldMean(Log.Column(column), 0, Log.Duration), report[item], RoundedTolerance, item);
        }

        // Writes part `part` of `parts` consecutive parts of the log, with the header, as a log of its own.
        private string WritePart(int part, int parts)
        {
            int rowsPerPart = Log.FrameCount / parts;
            int first = part * rowsPerPart;
            int last = part == parts - 1 ? Log.FrameCount : first + rowsPerPart;
            var path = Path.Combine(_folder, $"FrameView_{ProcessName}_part{part}_Log.csv");
            File.WriteAllLines(path, Log.Lines.Take(1).Concat(Log.Lines.Skip(1 + first).Take(last - first)));
            return path;
        }

        // Writes the log after `edit` changed its rows (cells and time of each row), keeping the first
        // `columns` columns, or all of them.
        private string WriteLog(string fileName, Action<string[], double> edit, int columns = int.MaxValue)
        {
            var lines = new List<string> { string.Join(",", Log.Lines[0].Split(',').Take(columns)) };
            for (int row = 0; row < Log.FrameCount; row++)
            {
                var cells = Log.Lines[row + 1].Split(',');
                edit(cells, Log.Times[row]);
                lines.Add(string.Join(",", cells.Take(columns)));
            }

            var path = Path.Combine(_folder, fileName);
            File.WriteAllLines(path, lines);
            return path;
        }

        /// <summary>
        /// Reads a FrameView log without the import: its columns as numbers (NaN for NA) and the frame
        /// times from TimeInSeconds, relative to the first frame. The log has no quoted cells.
        /// </summary>
        protected sealed class FrameViewLog
        {
            private string[] _header;
            private string[][] _rows;

            public string[] Lines { get; private set; }
            public double[] Times { get; private set; }
            public int FrameCount => _rows.Length;
            public double Duration => Times[Times.Length - 1];

            public static FrameViewLog Read(string path)
            {
                var log = new FrameViewLog { Lines = File.ReadAllLines(path) };
                log._header = log.Lines[0].Split(',').Select(column => column.Trim()).ToArray();
                log._rows = log.Lines.Skip(1).Select(line => line.Split(',')).ToArray();
                var times = log.Column("TimeInSeconds");
                log.Times = times.Select(time => time - times[0]).ToArray();
                return log;
            }

            public int Index(string column)
            {
                int index = Array.IndexOf(_header, column);
                Assert.IsTrue(index >= 0, $"the log has no column {column}");
                return index;
            }

            public double[] Column(string column)
            {
                int index = Index(column);
                return _rows.Select(row => double.TryParse(row[index], NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                    ? value : double.NaN).ToArray();
            }

            // Mean over [from, to) of the readings as FrameView logs them: each one holds until the
            // next frame.
            public double HoldMean(double[] values, double from, double to)
            {
                double sum = 0;
                double weight = 0;
                for (int i = 0; i < values.Length - 1; i++)
                {
                    double overlap = Math.Min(to, Times[i + 1]) - Math.Max(from, Times[i]);
                    if (overlap > 0)
                    {
                        sum += values[i] * overlap;
                        weight += overlap;
                    }
                }

                return sum / weight;
            }

            // Mean over [from, to) with each frame value standing for the span halfway to its
            // neighbours and the first and last one extending without limit: how PMD samples are put
            // onto a capture's grid (PmdTimeSeriesResampler).
            public double CellMean(double[] values, double from, double to)
            {
                double sum = 0;
                double weight = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    double spanStart = i == 0 ? double.NegativeInfinity : (Times[i - 1] + Times[i]) / 2;
                    double spanEnd = i == values.Length - 1 ? double.PositiveInfinity : (Times[i] + Times[i + 1]) / 2;
                    double overlap = Math.Min(to, spanEnd) - Math.Max(from, spanStart);
                    if (overlap > 0)
                    {
                        sum += values[i] * overlap;
                        weight += overlap;
                    }
                }

                return sum / weight;
            }
        }
    }
}
