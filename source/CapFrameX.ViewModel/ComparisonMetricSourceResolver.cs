using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;

namespace CapFrameX.ViewModel
{
    public static class ComparisonMetricSourceResolver
    {
        public static bool ShouldUseDisplayChangeMetrics(bool requested, IEnumerable<ISession> sessions)
        {
            if (!requested || sessions == null)
                return false;

            bool hasSession = false;
            foreach (ISession session in sessions)
            {
                hasSession = true;
                if (!HasValidDisplayChangeSample(session))
                    return false;
            }

            return hasSession;
        }

        private static bool HasValidDisplayChangeSample(ISession session)
        {
            if (session?.Runs == null)
                return false;

            bool hasCaptureData = false;
            foreach (ISessionRun run in session.Runs)
            {
                double[] presentTimes = run?.CaptureData?.MsBetweenPresents;
                if (!HasValidTimingSample(presentTimes))
                    continue;

                hasCaptureData = true;
                double[] displayChangeTimes = run?.CaptureData?.MsBetweenDisplayChange;
                if (!HasValidTimingSample(displayChangeTimes))
                    return false;
            }

            return hasCaptureData;
        }

        private static bool HasValidTimingSample(double[] values)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                double value = values[i];
                if (value > 0 && !double.IsNaN(value) && !double.IsInfinity(value))
                    return true;
            }

            return false;
        }
    }
}
