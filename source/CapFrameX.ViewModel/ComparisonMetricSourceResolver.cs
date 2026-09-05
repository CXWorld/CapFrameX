using CapFrameX.Data.Session.Contracts;
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
                if (!HasValidDisplayChangeSamples(session))
                    return false;
            }

            return hasSession;
        }

        private static bool HasValidDisplayChangeSamples(ISession session)
        {
            if (session?.Runs == null || session.Runs.Count == 0)
                return false;

            foreach (ISessionRun run in session.Runs)
            {
                if (!HasValidTimingSample(run?.CaptureData?.MsBetweenPresents)
                    || !HasValidTimingSample(run?.CaptureData?.MsBetweenDisplayChange))
                {
                    return false;
                }
            }

            return true;
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
