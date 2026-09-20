using System;
using System.Collections.Generic;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class InjectionRetryBackoffTest
    {
        private const long TimestampFrequency = 1000;

        [TestMethod]
        public void RecordFailure_BlocksUntilDeadlineAndThenEscalates()
        {
            long timestamp = 0;
            var backoff = CreateBackoff(() => timestamp);

            Assert.IsFalse(backoff.IsBlocked(42));
            Assert.AreEqual(TimeSpan.FromSeconds(1), backoff.RecordFailure(42));
            Assert.IsTrue(backoff.IsBlocked(42));

            timestamp = TimestampFrequency - 1;
            Assert.IsTrue(backoff.IsBlocked(42));

            timestamp = TimestampFrequency;
            Assert.IsFalse(backoff.IsBlocked(42));
            Assert.AreEqual(TimeSpan.FromSeconds(2), backoff.RecordFailure(42));
            Assert.IsTrue(backoff.IsBlocked(42));

            timestamp += 2 * TimestampFrequency;
            Assert.IsFalse(backoff.IsBlocked(42));
        }

        [TestMethod]
        public void RecordFailure_UsesExponentialDelayCappedAtThirtySeconds()
        {
            long timestamp = 0;
            var backoff = CreateBackoff(() => timestamp);
            var actualDelays = new List<TimeSpan>();

            for (int failure = 0; failure < 8; failure++)
            {
                TimeSpan delay = backoff.RecordFailure(42);
                actualDelays.Add(delay);
                timestamp += (long)delay.TotalSeconds * TimestampFrequency;
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(4),
                    TimeSpan.FromSeconds(8),
                    TimeSpan.FromSeconds(16),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30)
                },
                actualDelays);
        }

        [TestMethod]
        public void Reset_ClearsOnlyTheSelectedPid()
        {
            long timestamp = 0;
            var backoff = CreateBackoff(() => timestamp);

            backoff.RecordFailure(42);
            backoff.RecordFailure(84);
            backoff.Reset(42);

            Assert.IsFalse(backoff.IsBlocked(42));
            Assert.IsTrue(backoff.IsBlocked(84));
            Assert.AreEqual(TimeSpan.FromSeconds(1), backoff.RecordFailure(42));
        }

        [TestMethod]
        public void Prune_RemovesRetryStateForExitedProcesses()
        {
            long timestamp = 0;
            var backoff = CreateBackoff(() => timestamp);

            backoff.RecordFailure(42);
            timestamp += TimestampFrequency;
            backoff.RecordFailure(42);
            backoff.RecordFailure(84);

            backoff.Prune(pid => pid == 84);

            Assert.IsFalse(backoff.IsBlocked(42));
            Assert.IsTrue(backoff.IsBlocked(84));
            Assert.AreEqual(TimeSpan.FromSeconds(1), backoff.RecordFailure(42));
        }

        private static InjectionRetryBackoff CreateBackoff(Func<long> timestampProvider)
        {
            return new InjectionRetryBackoff(timestampProvider, TimestampFrequency);
        }
    }
}
