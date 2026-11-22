using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// One completed run of the run history, or its aggregated result: the average FPS and the two
    /// further metrics of the run history (<see cref="SecondMetric"/>, <see cref="ThirdMetric"/>, EMetric
    /// names as the capture settings store them, "None" when unset), all in FPS.
    /// </summary>
    public sealed class RunHistoryRun
    {
        public RunHistoryRun(double average, double second, double third, string secondMetric, string thirdMetric,
            double deviation = double.NaN, bool isOutlier = false)
        {
            Average = average;
            Second = second;
            Third = third;
            SecondMetric = secondMetric;
            ThirdMetric = thirdMetric;
            Deviation = deviation;
            IsOutlier = isOutlier;
        }

        public double Average { get; }
        public double Second { get; }
        public double Third { get; }
        public string SecondMetric { get; }
        public string ThirdMetric { get; }
        /// <summary>
        /// The run's difference to the median of every completed run in percent, in the metric the outlier
        /// detection compares (RelatedMetricOverlay); NaN with fewer than two runs.
        /// </summary>
        public double Deviation { get; }
        public bool IsOutlier { get; }
    }

    /// <summary>
    /// The run history as one consistent picture: the runs of a session, the completed ones with their
    /// metrics, outlier flags and deviations, and the aggregated result once there is one.
    /// </summary>
    public sealed class RunHistorySnapshot
    {
        public static RunHistorySnapshot Empty { get; } = new RunHistorySnapshot(0, "None", "None", "Average",
            Array.Empty<RunHistoryRun>(), null);

        public RunHistorySnapshot(int slots, string secondMetric, string thirdMetric, string referenceMetric,
            IReadOnlyList<RunHistoryRun> runs, RunHistoryRun result)
        {
            Slots = slots;
            SecondMetric = secondMetric;
            ThirdMetric = thirdMetric;
            ReferenceMetric = referenceMetric;
            Runs = runs ?? Array.Empty<RunHistoryRun>();
            Result = result;
        }

        /// <summary>The runs of a session (SelectedHistoryRuns).</summary>
        public int Slots { get; }
        /// <summary>The metrics the run history shows now; runs taken before a change keep their own.</summary>
        public string SecondMetric { get; }
        public string ThirdMetric { get; }
        /// <summary>The metric the deviations and the outlier detection compare: "Average", "Second" or "Third".</summary>
        public string ReferenceMetric { get; }
        /// <summary>The completed runs, oldest first.</summary>
        public IReadOnlyList<RunHistoryRun> Runs { get; }
        /// <summary>The aggregated result, null until the aggregation produced one.</summary>
        public RunHistoryRun Result { get; }
    }
}
