using CapFrameX.PresentMonInterface;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class PresentMonProcessListTest
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void Stop_EndsTheRowThread()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);

            context.List.Stop();

            Assert.AreEqual(1, context.Threads.Count);
            Assert.IsTrue(context.Threads[0].Join(WaitTimeout), "The row thread must end with the capture service.");
        }

        [TestMethod]
        public void RepeatedRestarts_LeaveNoRowThreadBehind()
        {
            // Regression: every RestartCaptureService left one parked EventLoopScheduler thread.
            using var context = new ListContext();
            const int restarts = 10;

            for (int i = 0; i < restarts; i++)
            {
                context.List.Start(context.Rows);
                context.PublishHeader();
                context.PublishRow("Game", 100 + i);
                context.WaitForProcess("Game", 100 + i);
                context.List.Stop();
            }

            Assert.AreEqual(restarts, context.Threads.Count);
            foreach (Thread thread in context.Threads)
                Assert.IsTrue(thread.Join(WaitTimeout), "A restart left a row thread running.");
        }

        [TestMethod]
        public void StartWithoutStop_ReleasesThePreviousSubscription()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);
            Thread firstThread = context.Threads.Single();

            context.List.Start(context.Rows);

            Assert.IsTrue(firstThread.Join(WaitTimeout), "The replaced row thread must end.");
            context.PublishHeader();
            context.PublishRow("Other", 200);
            context.WaitForProcess("Other", 200);
            Assert.AreEqual(2, context.Threads.Count);
            Assert.IsTrue(context.Threads[1].IsAlive, "The new subscription keeps its own thread.");
        }

        [TestMethod]
        public void StartWithoutStop_KeepsTheKnownProcesses()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);

            context.List.Start(context.Rows);

            CollectionAssert.AreEqual(new[] { ("Game", 100) }, context.Processes());
        }

        [TestMethod]
        public void Stop_ClearsTheList()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);

            context.List.Stop();

            Assert.AreEqual(0, context.Processes().Length);
        }

        [TestMethod]
        public void Stop_BeforeAnyStart_DoesNothing()
        {
            using var context = new ListContext();

            context.List.Stop();
            context.List.Stop();

            Assert.AreEqual(0, context.Processes().Length);
            Assert.AreEqual(0, context.Threads.Count);
        }

        [TestMethod]
        public void RowsAfterStop_AreIgnored()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.List.Stop();

            context.PublishRow("Game", 100);

            Assert.AreEqual(0, context.Processes().Length);
            Assert.AreEqual(0, context.Threads.Count, "No row reached a scheduler.");
        }

        [TestMethod]
        public void Rows_SkipTheHeaderAndStripTheExeSuffix()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);

            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.PublishRow("Game", 100);
            context.PublishRow("Other.exe", 200);
            context.WaitForProcess("Other", 200);

            CollectionAssert.AreEquivalent(new[] { ("Game", 100), ("Other", 200) }, context.Processes());
        }

        [TestMethod]
        public void RowWithoutAProcessId_IsIgnored()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();

            context.Rows.OnNext(ListContext.Row("Broken.exe", "not-a-pid"));
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);

            CollectionAssert.AreEqual(new[] { ("Game", 100) }, context.Processes());
        }

        [TestMethod]
        public void PrunePass_DropsExitedProcessesOncePerSecond()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.PublishRow("Exited", 200);
            context.WaitForProcess("Exited", 200);
            context.ExitedPids.Add(200);

            context.Clock.AdvanceBy(TimeSpan.FromMilliseconds(999));
            Assert.AreEqual(2, context.Processes().Length, "No pass before a second has elapsed.");

            context.Clock.AdvanceBy(TimeSpan.FromMilliseconds(1));
            CollectionAssert.AreEqual(new[] { ("Game", 100) }, context.Processes());
        }

        [TestMethod]
        public void PrunePass_StopsWithTheList()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.List.Stop();

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(10));

            Assert.AreEqual(0, context.AliveChecks);
        }

        [TestMethod]
        public void PrunePass_RunsOnlyForTheCurrentStart()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);
            int checksBeforeThePass = context.AliveChecks;

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.AreEqual(checksBeforeThePass + 1, context.AliveChecks,
                "A replaced start must not keep its own pass.");
        }

        [TestMethod]
        public void LateRowOfAnExitedProcess_DoesNotListItAgain()
        {
            // 2026-10-25: nine seconds after cfx_d3d11_x64 closed, PresentMon delivered one more
            // row of its PID under the name "<unknown>". Listed beside Metro until the next pass,
            // it left the automatic target selection with two candidates and hid the overlay.
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("metro", 100);
            context.PublishRow("cfx_d3d11_x64", 200);
            context.WaitForProcess("cfx_d3d11_x64", 200);
            context.ExitedPids.Add(200);
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));
            CollectionAssert.AreEqual(new[] { ("metro", 100) }, context.Processes());

            context.PublishRow("<unknown>", 200);
            context.PublishRow("cfx_d3d11_x64", 200);
            // Rows are handled in order on one thread: once this one is listed, the late ones were.
            context.PublishRow("Later", 300);
            context.WaitForProcess("Later", 300);

            CollectionAssert.AreEquivalent(new[] { ("metro", 100), ("Later", 300) }, context.Processes());
        }

        [TestMethod]
        public void LateRows_CostOneCheckPerPass()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.ExitedPids.Add(200);

            for (int i = 0; i < 5; i++)
                context.PublishRow("<unknown>", 200);
            context.PublishRow("Game", 100);
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);
            Assert.AreEqual(2, context.AliveChecks,
                "One check for the exited PID and one for the new entry; a listed process costs none.");

            // The pass checks the game and forgets the exited rows, so a reused PID gets its chance.
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));
            context.PublishRow("<unknown>", 200);
            context.PublishRow("Later", 300);
            context.WaitForProcess("Later", 300);

            Assert.AreEqual(5, context.AliveChecks);
            CollectionAssert.AreEquivalent(new[] { ("Game", 100), ("Later", 300) }, context.Processes());
        }

        [TestMethod]
        public void Filter_ExcludesTheGivenProcessNames()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.PublishRow("dwm", 200);
            context.WaitForProcess("dwm", 200);

            var processes = context.List.GetAllFilteredProcesses(new HashSet<string> { "dwm" }).ToArray();

            CollectionAssert.AreEqual(new[] { ("Game", 100) }, processes);
        }

        [TestMethod]
        public void Filter_UsesTheComparerOfTheGivenSet()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.PublishRow("dwm", 200);
            context.WaitForProcess("dwm", 200);

            var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DWM" };
            var processes = context.List.GetAllFilteredProcesses(filter).ToArray();

            CollectionAssert.AreEqual(new[] { ("Game", 100) }, processes);
        }

        [TestMethod]
        public void FilteredProcesses_AreASnapshotThatLaterRowsDoNotChange()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);

            var snapshot = context.List.GetAllFilteredProcesses(new HashSet<string>());
            context.PublishRow("Other", 200);
            context.WaitForProcess("Other", 200);

            CollectionAssert.AreEqual(new[] { ("Game", 100) }, snapshot.ToArray());
        }

        [TestMethod]
        public void FilteredProcesses_SurviveARowAddedWhileTheyAreEnumerated()
        {
            // Regression: the UI thread enumerates the list every second while the row thread
            // adds a newly reported process, which failed with "Collection was modified".
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.PublishRow("Launcher", 150);
            context.WaitForProcess("Launcher", 150);

            var seen = new List<(string, int)>();
            using (var enumerator = context.List.GetAllFilteredProcesses(new HashSet<string>()).GetEnumerator())
            {
                Assert.IsTrue(enumerator.MoveNext());
                seen.Add(enumerator.Current);
                context.PublishRow("Other", 200);
                context.WaitForProcess("Other", 200);
                while (enumerator.MoveNext()) seen.Add(enumerator.Current);
            }

            CollectionAssert.AreEquivalent(new[] { ("Game", 100), ("Launcher", 150) }, seen);
        }

        [TestMethod]
        public void FilteredProcesses_KeepTheirSnapshotAcrossStop()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            context.PublishRow("Game", 100);
            context.WaitForProcess("Game", 100);

            var snapshot = context.List.GetAllFilteredProcesses(new HashSet<string>());
            context.List.Stop();

            CollectionAssert.AreEqual(new[] { ("Game", 100) }, snapshot.ToArray());
            Assert.AreEqual(0, context.Processes().Length);
        }

        [TestMethod]
        public void FilteredProcesses_CanBeEnumeratedWhileRowsKeepArriving()
        {
            const int processCount = 20_000;
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishHeader();
            using var stop = new CancellationTokenSource();
            var producer = new Thread(() =>
            {
                // Every row is a new process, so every one of them modifies the set.
                for (int pid = 1; pid <= processCount && !stop.IsCancellationRequested; pid++)
                {
                    context.PublishRow("Game", pid);
                    if (pid % 64 == 0) Thread.Sleep(0);
                }
            }) { IsBackground = true };
            producer.Start();

            int seen = 0;
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (seen < processCount && watch.Elapsed < TimeSpan.FromSeconds(10))
                {
                    seen = 0;
                    foreach (var process in context.List.GetAllFilteredProcesses(new HashSet<string>()))
                        seen++;
                }
            }
            finally
            {
                // A failed enumeration must not leave the producer writing into the disposed
                // subject, which would take down the test host.
                stop.Cancel();
                producer.Join(WaitTimeout);
            }

            Assert.AreEqual(processCount, seen);
        }

        [TestMethod]
        public void Activity_CountsPresentsAndGpuBusyOfCompletedSeconds()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader();
            for (int i = 0; i < 60; i++)
                context.PublishPresent("Game", 100, gpuBusyMs: 5);
            context.Flush();

            var filling = context.Activity("Game");
            Assert.AreEqual(0.0, filling.PresentsPerSecond, "The current second is still filling.");
            Assert.IsNull(filling.GpuBusyShare, "No second is complete yet.");
            Assert.AreEqual(0, filling.IdleSeconds);

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            var measured = context.Activity("Game");
            Assert.AreEqual(60, measured.PresentsPerSecond);
            Assert.AreEqual(0.3, measured.GpuBusyShare.Value, 1e-9);
            Assert.AreEqual(1, measured.IdleSeconds);
        }

        [TestMethod]
        public void Activity_AveragesOverTheWindowAndForgetsOlderSeconds()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader();
            for (int i = 0; i < 10; i++)
                context.PublishPresent("Game", 100, gpuBusyMs: 10);
            context.Flush();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));
            for (int i = 0; i < 30; i++)
                context.PublishPresent("Game", 100, gpuBusyMs: 10);
            context.Flush();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            var twoSeconds = context.Activity("Game");
            Assert.AreEqual(20, twoSeconds.PresentsPerSecond);
            Assert.AreEqual(0.2, twoSeconds.GpuBusyShare.Value, 1e-9);

            // The window covers the second with 30 presents and two without any.
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(2));
            var window = context.Activity("Game");
            Assert.AreEqual(3, PresentMonProcessList.ActivityWindowSeconds, "The expectations assume a 3 s window.");
            Assert.AreEqual(10, window.PresentsPerSecond);
            Assert.AreEqual(0.1, window.GpuBusyShare.Value, 1e-9);
            Assert.AreEqual(3, window.IdleSeconds);

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));
            var idle = context.Activity("Game");
            Assert.AreEqual(0.0, idle.PresentsPerSecond);
            Assert.AreEqual(0.0, idle.GpuBusyShare.Value);
            Assert.AreEqual(4, idle.IdleSeconds);
        }

        [TestMethod]
        public void Activity_OfALaterProcess_CountsOnlyTheSecondsItWasListed()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(2));

            for (int i = 0; i < 20; i++)
                context.PublishPresent("Game", 100, gpuBusyMs: 10);
            context.Flush();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            var activity = context.Activity("Game");
            Assert.AreEqual(20, activity.PresentsPerSecond);
            Assert.AreEqual(0.2, activity.GpuBusyShare.Value, 1e-9);
        }

        [TestMethod]
        public void Activity_CapsTheGpuShareOfSeveralSwapChains()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader();
            for (int i = 0; i < 240; i++)
                context.PublishPresent("Game", 100, gpuBusyMs: 6);
            context.Flush();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.AreEqual(1.0, context.Activity("Game").GpuBusyShare.Value);
        }

        [TestMethod]
        public void Activity_WithoutAGpuBusyColumn_HasNoGpuShare()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader(withGpuBusy: false);
            context.PublishPresent("Game", 100, gpuBusyMs: 10);
            context.Flush();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            var activity = context.Activity("Game");
            Assert.AreEqual(1, activity.PresentsPerSecond);
            Assert.IsNull(activity.GpuBusyShare);
        }

        [TestMethod]
        public void Activity_KeepsTheLatestPresentModeAndSeesGeneratedFrames()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader();
            context.PublishPresent("Game", 100, gpuBusyMs: 1, presentMode: "Composed: Flip");
            context.PublishPresent("Game", 100, gpuBusyMs: 1, presentMode: "Hardware: Independent Flip",
                frameType: "Intel XeSS-FG");
            context.PublishPresent("Browser", 200, gpuBusyMs: 1, presentMode: "Composed: Flip");
            context.Flush();
            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.AreEqual("Hardware: Independent Flip", context.Activity("Game").PresentMode);
            Assert.IsTrue(context.Activity("Game").HasGeneratedFrames);
            Assert.IsFalse(context.Activity("Browser").HasGeneratedFrames);

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(PresentMonProcessList.ActivityWindowSeconds));
            Assert.IsFalse(context.Activity("Game").HasGeneratedFrames, "The generated frame left the window.");
            Assert.AreEqual("Hardware: Independent Flip", context.Activity("Game").PresentMode);
        }

        [TestMethod]
        public void Activity_DropsTheProcessesThatExited()
        {
            using var context = new ListContext();
            context.List.Start(context.Rows);
            context.PublishActivityHeader();
            context.PublishPresent("Game", 100, gpuBusyMs: 1);
            context.PublishPresent("Exited", 200, gpuBusyMs: 1);
            context.Flush();
            context.ExitedPids.Add(200);

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.IsFalse(context.List.GetProcessActivity().Any(activity => activity.ProcessId == 200));
            Assert.AreEqual(1, context.Activity("Game").PresentsPerSecond);
        }

        [TestMethod]
        [DataRow("Application", false)]
        [DataRow("NotSet", false)]
        [DataRow("Unspecified", false)]
        [DataRow("Repeated", false)]
        [DataRow("NA", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [DataRow("Intel XeSS-FG", true)]
        [DataRow("AMD AFMF", true)]
        public void IsGeneratedFrame(string frameType, bool expected)
        {
            Assert.AreEqual(expected, PresentMonProcessList.IsGeneratedFrame(frameType));
        }

        private sealed class ListContext : IDisposable
        {
            private readonly object _threadsLock = new object();
            private readonly List<Thread> _threads = new List<Thread>();
            private int _aliveChecks;
            private int _flushes;

            public readonly Subject<string[]> Rows = new Subject<string[]>();
            public readonly HistoricalScheduler Clock = new HistoricalScheduler();
            public readonly HashSet<int> ExitedPids = new HashSet<int>();
            public readonly PresentMonProcessList List;

            public ListContext()
            {
                List = new PresentMonProcessList(NullLogger.Instance, IsAlive,
                    () => new EventLoopScheduler(start =>
                    {
                        var thread = new Thread(start) { IsBackground = true, Name = "Process list test" };
                        lock (_threadsLock) _threads.Add(thread);
                        return thread;
                    }),
                    Clock);
            }

            public List<Thread> Threads
            {
                get { lock (_threadsLock) return _threads.ToList(); }
            }

            public int AliveChecks => Volatile.Read(ref _aliveChecks);

            public static string[] Row(string application, string processId)
            {
                var row = new string[Math.Max(PresentMonCaptureService.ApplicationName_INDEX,
                    PresentMonCaptureService.ProcessID_INDEX) + 1];
                row[PresentMonCaptureService.ApplicationName_INDEX] = application;
                row[PresentMonCaptureService.ProcessID_INDEX] = processId;
                return row;
            }

            public void PublishHeader() => Rows.OnNext(Row("Application", "ProcessID"));

            public void PublishRow(string application, int processId)
                => Rows.OnNext(Row(application, processId.ToString()));

            // The statistics columns follow the two the list always reads; the list finds them
            // by their header names.
            private static int StatisticsColumn => Math.Max(PresentMonCaptureService.ApplicationName_INDEX,
                PresentMonCaptureService.ProcessID_INDEX) + 1;

            public void PublishActivityHeader(bool withGpuBusy = true)
            {
                var header = ActivityRow("Application", "ProcessID");
                header[StatisticsColumn] = "PresentMode";
                header[StatisticsColumn + 1] = "FrameType";
                header[StatisticsColumn + 2] = withGpuBusy ? "MsGPUBusy" : "MsGPUTime";
                Rows.OnNext(header);
            }

            public void PublishPresent(string application, int processId, double gpuBusyMs,
                string presentMode = "Composed: Flip", string frameType = "Application")
            {
                var row = ActivityRow(application, processId.ToString());
                row[StatisticsColumn] = presentMode;
                row[StatisticsColumn + 1] = frameType;
                row[StatisticsColumn + 2] = gpuBusyMs.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Rows.OnNext(row);
            }

            /// <summary>
            /// Waits until the row thread handled every row published so far: rows are handled in
            /// order on one thread, so a new process that is listed was preceded by all of them.
            /// </summary>
            public void Flush()
            {
                int processId = 900_000 + Interlocked.Increment(ref _flushes);
                PublishRow("Flush", processId);
                WaitForProcess("Flush", processId);
            }

            public CapFrameX.Capture.Contracts.CaptureProcessActivity Activity(string name)
                => List.GetProcessActivity().Single(activity => activity.Name == name);

            private static string[] ActivityRow(string application, string processId)
            {
                var row = new string[StatisticsColumn + 3];
                row[PresentMonCaptureService.ApplicationName_INDEX] = application;
                row[PresentMonCaptureService.ProcessID_INDEX] = processId;
                return row;
            }

            public (string, int)[] Processes()
                => List.GetAllFilteredProcesses(new HashSet<string>()).ToArray();

            public void WaitForProcess(string name, int processId)
            {
                Assert.IsTrue(SpinWait.SpinUntil(() => Processes().Contains((name, processId)), WaitTimeout),
                    $"{name} ({processId}) never reached the list.");
            }

            private bool IsAlive(int processId)
            {
                Interlocked.Increment(ref _aliveChecks);
                return !ExitedPids.Contains(processId);
            }

            public void Dispose()
            {
                List.Stop();
                Rows.Dispose();
            }
        }
    }
}
