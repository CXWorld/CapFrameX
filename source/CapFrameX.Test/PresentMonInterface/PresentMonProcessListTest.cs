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

            context.Clock.AdvanceBy(TimeSpan.FromSeconds(1));

            Assert.AreEqual(1, context.AliveChecks, "A replaced start must not keep its own pass.");
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

        private sealed class ListContext : IDisposable
        {
            private readonly object _threadsLock = new object();
            private readonly List<Thread> _threads = new List<Thread>();
            private int _aliveChecks;

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

            public (string, int)[] Processes()
                => List.GetAllFilteredProcesses(new HashSet<string>()).ToArray();

            public void WaitForProcess(string name, int processId)
            {
                Assert.IsTrue(SpinWait.SpinUntil(() => ContainsWhileRowsArrive(name, processId), WaitTimeout),
                    $"{name} ({processId}) never reached the list.");
            }

            // The list is enumerated after its lock is released, so a row added meanwhile can
            // fail the enumeration (audit finding 13); retry until it is fixed.
            private bool ContainsWhileRowsArrive(string name, int processId)
            {
                try
                {
                    return Processes().Contains((name, processId));
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
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
