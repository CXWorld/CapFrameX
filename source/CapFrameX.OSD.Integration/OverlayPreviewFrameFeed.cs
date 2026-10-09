using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace CapFrameX.OSD.Integration
{
    public readonly struct OverlayPreviewFrame
    {
        public bool IsReset { get; }
        public double TimeMs { get; }
        public double FrametimeMs { get; }
        public double DisplayTimeMs { get; }

        private OverlayPreviewFrame(bool reset, double time, double frametime, double displayTime)
        {
            IsReset = reset;
            TimeMs = time;
            FrametimeMs = frametime;
            DisplayTimeMs = displayTime;
        }

        internal static OverlayPreviewFrame Reset() => new OverlayPreviewFrame(true, 0, 0, 0);
        internal static OverlayPreviewFrame Sample(double time, double frametime, double displayTime)
            => new OverlayPreviewFrame(false, time, frametime, displayTime);
    }

    /// <summary>
    /// A cold, selection-scoped stream for designer graphs. Subscribing observes the existing
    /// capture feed; it never starts another PresentMon process or writes to the in-game OSD.
    /// A target change clears history before any sample from the new target is published.
    /// </summary>
    public static class OverlayPreviewFrameFeed
    {
        public static IObservable<OverlayPreviewFrame> Observe(IObservable<string[]> frames,
            IObservable<int> targetProcessIds, int processIdColumn, int frametimeColumn,
            int displayTimeColumn, Func<int> startTimeColumn)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (targetProcessIds == null) throw new ArgumentNullException(nameof(targetProcessIds));
            if (startTimeColumn == null) throw new ArgumentNullException(nameof(startTimeColumn));
            if (processIdColumn < 0) throw new ArgumentOutOfRangeException(nameof(processIdColumn));
            if (frametimeColumn < 0) throw new ArgumentOutOfRangeException(nameof(frametimeColumn));

            return Observable.Create<OverlayPreviewFrame>(observer =>
            {
                var gate = new object();
                int target = 0;
                double previousTime = 0;
                var subscriptions = new CompositeDisposable();
                subscriptions.Add(targetProcessIds.DistinctUntilChanged().Subscribe(pid =>
                {
                    lock (gate)
                    {
                        target = Math.Max(0, pid);
                        previousTime = 0;
                        observer.OnNext(OverlayPreviewFrame.Reset());
                    }
                }, observer.OnError));
                subscriptions.Add(frames.Subscribe(row =>
                {
                    lock (gate)
                    {
                        if (!PresentMonFrameFilter.IsForTargetProcess(row, processIdColumn, target)) return;
                        double time = ReadPositive(row, startTimeColumn());
                        if (time <= previousTime) return;
                        double frametime = ReadPositive(row, frametimeColumn);
                        double displayTime = ReadPositive(row, displayTimeColumn);
                        if (frametime >= 10000) frametime = 0;
                        if (displayTime >= 10000) displayTime = 0;
                        if (frametime == 0 && displayTime == 0) return;
                        previousTime = time;
                        observer.OnNext(OverlayPreviewFrame.Sample(time, frametime, displayTime));
                    }
                }, observer.OnError, observer.OnCompleted));
                return subscriptions;
            });
        }

        private static double ReadPositive(string[] row, int index)
        {
            return index >= 0 && index < row.Length &&
                double.TryParse(row[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
                !double.IsNaN(value) && !double.IsInfinity(value) && value > 0 ? value : 0;
        }
    }
}
