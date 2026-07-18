using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class ComparisonMetricSourceResolverTest
    {
        [TestMethod]
        public void ShouldUseDisplayChangeMetrics_AllSessionsHaveDisplayData_ReturnsTrue()
        {
            var sessions = new[] { CreateSession(16.6, 16.7), CreateSession(8.3, 8.4) };

            Assert.IsTrue(ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true, sessions));
        }

        [TestMethod]
        public void ShouldUseDisplayChangeMetrics_OneSessionHasNoDisplayData_ReturnsFalse()
        {
            var sessions = new[]
            {
                CreateSession(16.6, 16.7),
                CreateSession(0, double.NaN, double.PositiveInfinity)
            };

            Assert.IsFalse(ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true, sessions));
        }

        [TestMethod]
        public void ShouldUseDisplayChangeMetrics_OneRunHasNoDisplayData_ReturnsFalse()
        {
            var session = CreateSession(16.6, 16.7);
            session.Runs.Add(CreateRun(0, 0));

            Assert.IsFalse(ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true,
                new[] { session }));
        }

        [TestMethod]
        public void ShouldUseDisplayChangeMetrics_NotRequestedOrNoSessions_ReturnsFalse()
        {
            Assert.IsFalse(ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(false,
                new[] { CreateSession(16.6) }));
            Assert.IsFalse(ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true,
                new ISession[0]));
        }

        private static ISession CreateSession(params double[] displayChangeTimes)
        {
            return new Session
            {
                Runs = new List<ISessionRun> { CreateRun(displayChangeTimes) }
            };
        }

        private static ISessionRun CreateRun(params double[] displayChangeTimes)
        {
            var presentTimes = new double[displayChangeTimes.Length];
            for (int i = 0; i < presentTimes.Length; i++)
                presentTimes[i] = 16.6;

            return new SessionRun
            {
                CaptureData = new SessionCaptureData(displayChangeTimes.Length)
                {
                    MsBetweenPresents = presentTimes,
                    MsBetweenDisplayChange = displayChangeTimes
                }
            };
        }
    }
}
