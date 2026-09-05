using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

namespace CapFrameX.Test.Logging
{
    [TestClass]
    [DoNotParallelize]
    public class ApplicationLoggingTest
    {
        private string _logDirectory;

        [TestInitialize]
        public void Initialize()
        {
            _logDirectory = Path.Combine(Path.GetTempPath(), "CapFrameX.LoggingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_logDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_logDirectory, true);
        }

        [TestMethod]
        public void DefaultLogging_PreservesCaptureMilestonesAndProblems()
        {
            using (var logger = ApplicationLogging.CreateLogger(_logDirectory))
            {
                for (int frame = 0; frame < 1000; frame++)
                    logger.Debug("Per-frame diagnostic {Frame}", frame);

                logger.Information("Capture started");
                logger.Information("Capture saved");
                logger.Warning("Capture warning");
                logger.Error("Capture error");
            }

            var lines = File.ReadAllLines(Path.Combine(_logDirectory, "CapFrameX.log"));
#if DEBUG
            Assert.AreEqual(1004, lines.Length);
#else
            Assert.AreEqual(4, lines.Length,
                "Release logs must keep capture milestones and problems without diagnostic rows.");
#endif
            Assert.IsTrue(lines.Any(line => line.Contains("Capture started")));
            Assert.IsTrue(lines.Any(line => line.Contains("Capture saved")));
            Assert.IsTrue(lines.Any(line => line.Contains("Capture warning")));
            Assert.IsTrue(lines.Any(line => line.Contains("Capture error")));
        }

        [TestMethod]
        public void FileRollover_KeepsOnlyTheConfiguredNumberOfBoundedFiles()
        {
            string payload = new string('x', 16 * 1024);
            int writesPerFile = (int)(ApplicationLogging.FileSizeLimitBytes / payload.Length) + 1;

            using (var logger = ApplicationLogging.CreateLogger(_logDirectory))
            {
                for (int index = 0; index < writesPerFile * (ApplicationLogging.RetainedFileCountLimit + 2); index++)
                    logger.Information("Log payload {Sequence} {Payload}", index, payload);

                logger.Information("Latest capture completed");
            }

            var files = new DirectoryInfo(_logDirectory).GetFiles("CapFrameX*.log");
            Assert.AreEqual(ApplicationLogging.RetainedFileCountLimit, files.Length);
            foreach (var file in files)
            {
                // Serilog completes an event before rolling, so allow one event beyond the limit.
                Assert.IsTrue(file.Length <= ApplicationLogging.FileSizeLimitBytes + payload.Length + 512,
                    $"Log file exceeded its size limit: {file.Name} ({file.Length} bytes).");
            }
            Assert.IsTrue(files.Any(file => File.ReadLines(file.FullName)
                .Any(line => line.Contains("Latest capture completed"))));
        }

        [TestMethod]
        public void InMemoryLog_KeepsRecentEventsAndProvidesAnIndependentSnapshot()
        {
            using var logger = new LoggerConfiguration().WriteTo.Sink(new InMemorySink()).CreateLogger();
            for (int index = 0; index < InMemorySink.MaxRetainedEvents + 100; index++)
                logger.Information("Event {Sequence}", index);

            var snapshot = InMemorySink.LogEvents;
            var events = snapshot.ToArray();
            Assert.AreEqual(InMemorySink.MaxRetainedEvents, events.Length);
            Assert.AreEqual("100", events[0].Properties["Sequence"].ToString());

            logger.Information("Final event");
            Assert.AreEqual(events.Length, snapshot.Count());
            Assert.AreEqual(events.Last(), snapshot.Last());
            Assert.AreEqual("Final event", InMemorySink.LogEvents.Last().MessageTemplate.Text);
        }

        [TestMethod]
        public void InMemoryLog_AllowsConcurrentWritersAndCrashReportSnapshots()
        {
            using var logger = new LoggerConfiguration().WriteTo.Sink(new InMemorySink()).CreateLogger();
            Parallel.For(0, 10000, index =>
            {
                logger.Information("Concurrent event {Sequence}", index);
                if (index % 20 == 0)
                    Assert.IsTrue(InMemorySink.LogEvents.Count() <= InMemorySink.MaxRetainedEvents);
            });

            var events = InMemorySink.LogEvents.ToArray();
            Assert.AreEqual(InMemorySink.MaxRetainedEvents, events.Length);
            Assert.AreEqual(events.Length, events.Select(item => item.Properties["Sequence"].ToString()).Distinct().Count());
        }
    }
}
