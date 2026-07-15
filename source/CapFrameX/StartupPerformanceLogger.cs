using System;

#if DEBUG
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Serilog;
#endif

namespace CapFrameX
{
    internal static class StartupPerformanceLogger
    {
#if DEBUG
        private static readonly object SyncRoot = new object();
        private static readonly List<TimingEntry> PendingEntries = new List<TimingEntry>();

        private static long _startupTimestamp;
        private static bool _loggerReady;
        private static int _sequence;

        public static void Start()
        {
            lock (SyncRoot)
            {
                _startupTimestamp = Stopwatch.GetTimestamp();
                _loggerReady = false;
                _sequence = 0;
                PendingEntries.Clear();
            }
        }

        public static IDisposable Measure(string stepName)
        {
            EnsureStarted();
            return new TimingScope(stepName);
        }

        public static void Mark(string milestoneName)
        {
            EnsureStarted();

            var timestamp = Stopwatch.GetTimestamp();
            Publish(new TimingEntry(
                Interlocked.Increment(ref _sequence),
                milestoneName,
                GetMilliseconds(_startupTimestamp, timestamp),
                0,
                GetMilliseconds(_startupTimestamp, timestamp),
                Thread.CurrentThread.ManagedThreadId,
                true));
        }

        public static void LoggerReady()
        {
            List<TimingEntry> entries;

            lock (SyncRoot)
            {
                _loggerReady = true;
                entries = new List<TimingEntry>(PendingEntries);
                PendingEntries.Clear();
            }

            Log.Debug("[StartupTiming] Debug-only startup timing enabled; all values use a monotonic clock");

            foreach (var entry in entries)
            {
                Write(entry);
            }
        }

        public static void Complete()
        {
            EnsureStarted();

            var completedTimestamp = Stopwatch.GetTimestamp();
            var totalMilliseconds = GetMilliseconds(_startupTimestamp, completedTimestamp);
            Publish(new TimingEntry(
                Interlocked.Increment(ref _sequence),
                "Application startup total",
                0,
                totalMilliseconds,
                totalMilliseconds,
                Thread.CurrentThread.ManagedThreadId,
                false));
        }

        private static void EnsureStarted()
        {
            if (Interlocked.Read(ref _startupTimestamp) == 0)
            {
                Start();
            }
        }

        private static void Publish(TimingEntry entry)
        {
            lock (SyncRoot)
            {
                if (!_loggerReady)
                {
                    PendingEntries.Add(entry);
                    return;
                }
            }

            Write(entry);
        }

        private static void Write(TimingEntry entry)
        {
            if (entry.IsMilestone)
            {
                Log.Debug(
                    "[StartupTiming] #{StartupSequence} milestone {StartupStep} reached at {CompletedMilliseconds:F2} ms (thread {ThreadId})",
                    entry.Sequence,
                    entry.StepName,
                    entry.CompletedMilliseconds,
                    entry.ThreadId);
                return;
            }

            Log.Debug(
                "[StartupTiming] #{StartupSequence} {StartupStep} completed in {ElapsedMilliseconds:F2} ms (timeline {StartMilliseconds:F2}-{CompletedMilliseconds:F2} ms, thread {ThreadId})",
                entry.Sequence,
                entry.StepName,
                entry.ElapsedMilliseconds,
                entry.StartMilliseconds,
                entry.CompletedMilliseconds,
                entry.ThreadId);
        }

        private static double GetMilliseconds(long startTimestamp, long endTimestamp)
        {
            return (endTimestamp - startTimestamp) * 1000d / Stopwatch.Frequency;
        }

        private sealed class TimingScope : IDisposable
        {
            private readonly string _stepName;
            private readonly long _startedTimestamp;
            private bool _disposed;

            public TimingScope(string stepName)
            {
                _stepName = stepName;
                _startedTimestamp = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                var completedTimestamp = Stopwatch.GetTimestamp();
                Publish(new TimingEntry(
                    Interlocked.Increment(ref _sequence),
                    _stepName,
                    GetMilliseconds(_startupTimestamp, _startedTimestamp),
                    GetMilliseconds(_startedTimestamp, completedTimestamp),
                    GetMilliseconds(_startupTimestamp, completedTimestamp),
                    Thread.CurrentThread.ManagedThreadId,
                    false));
            }
        }

        private sealed class TimingEntry
        {
            public TimingEntry(
                int sequence,
                string stepName,
                double startMilliseconds,
                double elapsedMilliseconds,
                double completedMilliseconds,
                int threadId,
                bool isMilestone)
            {
                Sequence = sequence;
                StepName = stepName;
                StartMilliseconds = startMilliseconds;
                ElapsedMilliseconds = elapsedMilliseconds;
                CompletedMilliseconds = completedMilliseconds;
                ThreadId = threadId;
                IsMilestone = isMilestone;
            }

            public int Sequence { get; }

            public string StepName { get; }

            public double StartMilliseconds { get; }

            public double ElapsedMilliseconds { get; }

            public double CompletedMilliseconds { get; }

            public int ThreadId { get; }

            public bool IsMilestone { get; }
        }
#else
        private static readonly IDisposable NoopScope = new EmptyScope();

        public static void Start()
        {
        }

        public static IDisposable Measure(string stepName)
        {
            return NoopScope;
        }

        public static void Mark(string milestoneName)
        {
        }

        public static void LoggerReady()
        {
        }

        public static void Complete()
        {
        }

        private sealed class EmptyScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
#endif
    }
}
