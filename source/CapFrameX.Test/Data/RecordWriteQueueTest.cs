using CapFrameX.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// Record rewrites run off the calling thread, one after the other, and the app can wait for
    /// them before it exits.
    /// </summary>
    [TestClass]
    public class RecordWriteQueueTest
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [TestMethod]
        public void Write_RunsOffTheCallingThread()
        {
            var queue = new RecordWriteQueue(null);
            int writeThread = -1;

            queue.Enqueue(() => writeThread = Environment.CurrentManagedThreadId);

            Assert.IsTrue(queue.WaitForPendingWrites(Timeout));
            Assert.AreNotEqual(-1, writeThread);
            Assert.AreNotEqual(Environment.CurrentManagedThreadId, writeThread);
        }

        [TestMethod]
        public void Writes_RunOneAfterTheOtherInQueueOrder()
        {
            var queue = new RecordWriteQueue(null);
            var order = new ConcurrentQueue<int>();
            int running = 0, overlaps = 0;

            for (int index = 0; index < 20; index++)
            {
                int number = index;
                queue.Enqueue(() =>
                {
                    if (Interlocked.Increment(ref running) > 1)
                        Interlocked.Increment(ref overlaps);
                    Thread.Sleep(2);
                    order.Enqueue(number);
                    Interlocked.Decrement(ref running);
                });
            }

            Assert.IsTrue(queue.WaitForPendingWrites(Timeout));
            Assert.AreEqual(0, overlaps, "Two writes ran at the same time.");
            CollectionAssert.AreEqual(Enumerable.Range(0, 20).ToArray(), order.ToArray());
        }

        [TestMethod]
        public void FailingWrite_IsLoggedAndTheNextOneStillRuns()
        {
            var logger = new Mock<ILogger<RecordWriteQueue>>();
            var queue = new RecordWriteQueue(logger.Object);
            bool nextRan = false;

            var failing = queue.Enqueue(() => throw new InvalidOperationException("write failed"));
            queue.Enqueue(() => nextRan = true);

            Assert.IsTrue(queue.WaitForPendingWrites(Timeout));
            Assert.IsTrue(nextRan);
            Assert.IsTrue(failing.IsCompletedSuccessfully, "A failing write must not fault its task.");
            logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<InvalidOperationException>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [TestMethod]
        public void WaitForPendingWrites_WaitsUntilTheLastQueuedWriteHasRun()
        {
            var queue = new RecordWriteQueue(null);
            using var gate = new ManualResetEventSlim();
            var done = new List<string>();
            queue.Enqueue(() => gate.Wait());
            var last = queue.Enqueue(() => done.Add("last"));

            Assert.IsFalse(queue.WaitForPendingWrites(TimeSpan.FromMilliseconds(50)));
            Assert.IsFalse(last.IsCompleted);

            gate.Set();

            Assert.IsTrue(queue.WaitForPendingWrites(Timeout));
            Assert.IsTrue(last.IsCompleted);
            CollectionAssert.AreEqual(new[] { "last" }, done);
        }

        [TestMethod]
        public void WaitForPendingWrites_WithNothingQueued_ReturnsAtOnce()
        {
            Assert.IsTrue(new RecordWriteQueue(null).WaitForPendingWrites(TimeSpan.Zero));
        }
    }
}
