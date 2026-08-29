using CapFrameX.Sensor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SingleFlightSensorPollerTest
    {
        [TestMethod]
        public void RequestWhilePollIsRunningIsDroppedWithoutCreatingBacklog()
        {
            using var pollStarted = new ManualResetEventSlim(false);
            using var releasePoll = new ManualResetEventSlim(false);
            using var snapshotPublished = new ManualResetEventSlim(false);
            int pollCount = 0;
            int publishedValue = 0;
            Exception pollingError = null;

            using var poller = new SingleFlightSensorPoller<int>(
                _ =>
                {
                    int currentPoll = Interlocked.Increment(ref pollCount);
                    pollStarted.Set();
                    if (!releasePoll.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("The test did not release the simulated sensor poll.");
                    return currentPoll;
                },
                value =>
                {
                    Volatile.Write(ref publishedValue, value);
                    snapshotPublished.Set();
                },
                exception => pollingError = exception,
                "Sensor poller test",
                ThreadPriority.BelowNormal);

            Assert.IsTrue(poller.TryRequest(forcePoll: false));
            Assert.IsTrue(pollStarted.Wait(TimeSpan.FromSeconds(5)));

            Assert.IsFalse(poller.TryRequest(forcePoll: false),
                "A tick arriving during hardware I/O must be dropped rather than queued.");

            releasePoll.Set();
            Assert.IsTrue(snapshotPublished.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(SpinWait.SpinUntil(() => !poller.IsBusy, TimeSpan.FromSeconds(5)));
            Assert.AreEqual(1, Volatile.Read(ref pollCount));
            Assert.AreEqual(1, Volatile.Read(ref publishedValue));
            Assert.IsNull(pollingError);

            pollStarted.Reset();
            snapshotPublished.Reset();
            Assert.IsTrue(poller.TryRequest(forcePoll: false),
                "The next real interval may start after the previous poll completed.");
            Assert.IsTrue(snapshotPublished.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(2, Volatile.Read(ref pollCount));
            Assert.AreEqual(2, Volatile.Read(ref publishedValue));
            Assert.IsNull(pollingError);
        }
    }
}
