using CapFrameX.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// CaptureManager disposes its capture and PMD schedulers right after their subscriptions,
    /// while the producer may still be inside OnNext. On 2026-10-25 that threw
    /// ObjectDisposedException out of CaptureManagerTest's frame emitter; in the app the producer
    /// is PresentMon's stdout reader.
    /// </summary>
    [TestClass]
    public class DisposeSafeEventLoopSchedulerTest
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void Schedule_RunsTheWorkOnTheLoopThread()
        {
            using var scheduler = new DisposeSafeEventLoopScheduler();
            var done = new ManualResetEventSlim();
            int callerThread = Environment.CurrentManagedThreadId;
            int loopThread = 0;
            IScheduler recursive = null;

            scheduler.Schedule(0, (self, _) =>
            {
                loopThread = Environment.CurrentManagedThreadId;
                recursive = self;
                done.Set();
                return Disposable.Empty;
            });

            Assert.IsTrue(done.Wait(Timeout));
            Assert.AreNotEqual(callerThread, loopThread);
            Assert.AreSame(scheduler, recursive, "recursive work goes through the guard too");
        }

        [TestMethod]
        public void WorkScheduledAfterDispose_IsDroppedInsteadOfThrown()
        {
            var plain = new EventLoopScheduler();
            plain.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => plain.Schedule(() => { }));

            var scheduler = new DisposeSafeEventLoopScheduler();
            scheduler.Dispose();
            bool ran = false;
            scheduler.Schedule(() => ran = true);
            scheduler.Schedule(TimeSpan.FromMilliseconds(1), () => ran = true);
            scheduler.Schedule(DateTimeOffset.Now, () => ran = true);
            Thread.Sleep(50);
            Assert.IsFalse(ran);
        }

        [TestMethod]
        public void ObserveOn_DisposedRightAfterItsSubscription_NeverThrowsIntoTheProducer()
        {
            var source = new Subject<int>();
            Exception producerFailure = null;
            using var stop = new CancellationTokenSource();
            Task producer = Task.Run(() =>
            {
                try
                {
                    while (!stop.IsCancellationRequested) source.OnNext(1);
                }
                catch (Exception e)
                {
                    producerFailure = e;
                }
            });

            // The order of CaptureManager.StopCapture: the subscription, then its scheduler.
            for (int i = 0; i < 500 && producerFailure == null; i++)
            {
                var scheduler = new DisposeSafeEventLoopScheduler();
                IDisposable subscription = source.ObserveOn(scheduler).Subscribe(_ => { });
                Thread.SpinWait(200);
                subscription.Dispose();
                scheduler.Dispose();
            }

            stop.Cancel();
            Assert.IsTrue(producer.Wait(Timeout));
            Assert.IsNull(producerFailure, producerFailure?.ToString());
        }
    }
}
