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
            var sessions = new[]
            {
                CreateSession(16.6, 16.7),
                CreateSession(8.3, 8.4)
            };

            bool result = ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true, sessions);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldUseDisplayChangeMetrics_OneSessionHasNoDisplayData_ReturnsFalse()
        {
            var sessions = new[]
            {
                CreateSession(16.6, 16.7),
                CreateSession(0, double.NaN, double.PositiveInfinity)
            };

            bool result = ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true, sessions);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldUseDisplayChangeMetrics_OneRunHasNoDisplayData_ReturnsFalse()
        {
            var session = CreateSession(16.6, 16.7);
            session.Runs.Add(CreateRun(0, 0));

            bool result = ComparisonMetricSourceResolver.ShouldUseDisplayChangeMetrics(true,
                new[] { session });

            Assert.IsFalse(result);
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
                Runs = new List<ISessionRun>
                {
                    CreateRun(displayChangeTimes)
                }
            };
        }

        private static ISessionRun CreateRun(params double[] displayChangeTimes)
        {
            var captureData = new SessionCaptureData(displayChangeTimes.Length)
            {
                MsBetweenDisplayChange = displayChangeTimes,
                MsBetweenPresents = CreatePresentTimes(displayChangeTimes.Length)
            };

            return new SessionRun
            {
                CaptureData = captureData
            };
        }

        private static double[] CreatePresentTimes(int count)
        {
            var presentTimes = new double[count];
            for (int i = 0; i < presentTimes.Length; i++)
                presentTimes[i] = 16.6;

            return presentTimes;
        }
    }
}
