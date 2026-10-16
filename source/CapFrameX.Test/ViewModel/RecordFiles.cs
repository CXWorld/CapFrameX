using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// Record files on disk and the Comparison and Report view models that load them through a
    /// real <see cref="RecordManager"/>.
    /// </summary>
    internal sealed class RecordFiles : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordFilesTests", Guid.NewGuid().ToString("N"));

        public RecordFiles()
        {
            Directory.CreateDirectory(_folder);
            RecordManager = new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null);
        }

        public RecordManager RecordManager { get; }

        /// <param name="kind">"missing", "empty file", "corrupt", "no frames" or "valid".</param>
        public IFileRecordInfo Record(string kind, string game = "Game", double[] frametimes = null)
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".json");
            switch (kind)
            {
                case "missing":
                    break;
                case "empty file":
                    File.WriteAllText(path, string.Empty);
                    break;
                case "corrupt":
                    File.WriteAllText(path, "{\"Hash\":\"broken\",\"Runs\":[{\"CaptureData\":");
                    break;
                case "no frames":
                    File.WriteAllText(path, JsonConvert.SerializeObject(new Session { Hash = "empty", Info = Info(game) }));
                    break;
                default:
                    File.WriteAllText(path, JsonConvert.SerializeObject(SessionWith(game, Run(frametimes))));
                    break;
            }

            var record = new Mock<IFileRecordInfo>();
            record.SetupGet(info => info.Id).Returns(path);
            record.SetupGet(info => info.FullPath).Returns(path);
            record.SetupGet(info => info.FileInfo).Returns(new FileInfo(path));
            record.SetupGet(info => info.GameName).Returns(game);
            record.SetupGet(info => info.ProcessName).Returns(game + ".exe");
            record.SetupGet(info => info.CreationDate).Returns("2026-10-16");
            record.SetupGet(info => info.CreationTime).Returns("12:00:00");
            return record.Object;
        }

        /// <summary>Frame times of a constant frame rate for the given number of seconds.</summary>
        public static double[] Constant(double frametime, double seconds)
            => Enumerable.Repeat(frametime, (int)(seconds * 1000 / frametime)).ToArray();

        public static Session SessionWith(string game, params ISessionRun[] runs)
            => new Session { Hash = Guid.NewGuid().ToString("N"), Info = Info(game), Runs = new List<ISessionRun>(runs) };

        public static SessionRun Run(double[] frametimes = null)
        {
            frametimes ??= Enumerable.Range(0, 240).Select(frame => 15d + frame % 4).ToArray();
            double time = 0;
            return new SessionRun
            {
                Hash = Guid.NewGuid().ToString("N"),
                CaptureData = new SessionCaptureData(frametimes.Length)
                {
                    MsBetweenPresents = frametimes,
                    MsBetweenDisplayChange = frametimes,
                    MsUntilDisplayed = frametimes,
                    TimeInSeconds = frametimes.Select(frametime => (time += frametime) / 1000).ToArray(),
                }
            };
        }

        public static FrametimeStatisticProvider StatisticProvider()
        {
            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.SetupGet(value => value.FpsValuesRoundingDigits).Returns(2);
            options.SetupGet(value => value.IntervalAverageWindowTime).Returns(500);
            return new FrametimeStatisticProvider(options.Object);
        }

        public ComparisonViewModel CreateComparison(ILogger<ComparisonViewModel> logger = null)
        {
            var frametimeAnalyzer = new Mock<IFrametimeAnalyzer>();
            frametimeAnalyzer.Setup(analyzer => analyzer.GetLShapeQuantiles(It.IsAny<ELShapeMetrics>()))
                .Returns(new[] { 50d, 99d });
            var model = new ComparisonViewModel(StatisticProvider(), frametimeAnalyzer.Object,
                new EventAggregator(), Configuration(), RecordManager, logger ?? NullLogger<ComparisonViewModel>.Instance);
            typeof(ComparisonViewModel).GetMethod("InitializePlotModels", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, null);
            return model;
        }

        public ReportViewModel CreateReport(IEventAggregator eventAggregator, bool showAverageRow = false)
        {
            var configuration = Configuration();
            configuration.ReportShowAverageRow = showAverageRow;
            var model = new ReportViewModel(StatisticProvider(), eventAggregator, configuration,
                RecordManager, NullLogger<ReportViewModel>.Instance);
            typeof(ReportViewModel).GetField("_useEventMessages", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(model, true);
            return model;
        }

        /// <summary>
        /// Runs an asynchronous view model method the way a drop runs it: started on this
        /// (dispatcher) thread, which its continuations come back to.
        /// </summary>
        public static void RunOnDispatcher(Func<Task> action)
        {
            var previous = SynchronizationContext.Current;
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            try
            {
                var frame = new DispatcherFrame();
                Task task = action();
                task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                    TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                task.GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        // Saved records always carry the app version; the version converter rejects a null one.
        private static SessionInfo Info(string game)
            => new SessionInfo { AppVersion = new Version(1, 9, 2, 0), Id = Guid.NewGuid(), GameName = game, ProcessName = game + ".exe" };

        private static CapFrameXConfiguration Configuration()
            => new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, new MemorySettings());

        // The view models read settings from background work while their constructors still run.
        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>();
            public Task Load() => Task.CompletedTask;
            public T GetValue<T>(string key) => (T)_values[key];
            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
}
