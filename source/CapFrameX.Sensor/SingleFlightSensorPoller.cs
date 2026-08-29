using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CapFrameX.Sensor
{
    /// <summary>
    /// Runs blocking hardware acquisition on one dedicated background thread.
    /// Requests received while a poll is queued or running are deliberately dropped: sensor
    /// data may be stale for one display cycle, but polling can neither overlap nor build a
    /// backlog which would later steal time from the game or overlay publisher.
    /// </summary>
    internal sealed class SingleFlightSensorPoller<T> : IDisposable
    {
        private readonly Action<Exception> _onError;
        private readonly Func<bool, T> _poll;
        private readonly Action<T> _publish;
        private readonly Thread _thread;
        private readonly AutoResetEvent _workAvailable = new AutoResetEvent(false);

        private int _disposed;
        private int _forcePoll;
        private int _queuedOrRunning;

        public SingleFlightSensorPoller(
            Func<bool, T> poll,
            Action<T> publish,
            Action<Exception> onError,
            string threadName,
            ThreadPriority priority)
        {
            _poll = poll ?? throw new ArgumentNullException(nameof(poll));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            _onError = onError;

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = string.IsNullOrWhiteSpace(threadName) ? "Sensor polling" : threadName,
                Priority = priority
            };
            _thread.Start();
        }

        internal bool IsBusy => Volatile.Read(ref _queuedOrRunning) != 0;

        /// <summary>
        /// Queues a poll without waiting. Returns false when another poll already owns the
        /// single-flight slot or shutdown has started.
        /// </summary>
        public bool TryRequest(bool forcePoll)
        {
            if (Volatile.Read(ref _disposed) != 0 ||
                Interlocked.CompareExchange(ref _queuedOrRunning, 1, 0) != 0)
            {
                return false;
            }

            Volatile.Write(ref _forcePoll, forcePoll ? 1 : 0);

            // Dispose can race the successful slot acquisition. Relinquish the slot instead of
            // waking a worker which is already terminating.
            if (Volatile.Read(ref _disposed) != 0)
            {
                Volatile.Write(ref _queuedOrRunning, 0);
                return false;
            }

            _workAvailable.Set();
            return true;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Never join here. A vendor API may be stalled inside the poll, and sensor teardown
            // must not turn that into an application-shutdown stall.
            _workAvailable.Set();
        }

        private void Run()
        {
            bool backgroundMode = SensorPollingThreadQoS.TryBegin();
            try
            {
                while (true)
                {
                    _workAvailable.WaitOne();
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        Volatile.Write(ref _queuedOrRunning, 0);
                        return;
                    }

                    try
                    {
                        T snapshot = _poll(Interlocked.Exchange(ref _forcePoll, 0) != 0);
                        if (Volatile.Read(ref _disposed) == 0)
                            _publish(snapshot);
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            _onError?.Invoke(exception);
                        }
                        catch
                        {
                            // Diagnostics must not terminate the long-lived polling worker.
                        }
                    }
                    finally
                    {
                        Volatile.Write(ref _queuedOrRunning, 0);
                    }
                }
            }
            finally
            {
                if (backgroundMode)
                    SensorPollingThreadQoS.End();
            }
        }
    }

    internal static class SensorPollingThreadQoS
    {
        // THREAD_MODE_BACKGROUND_BEGIN lowers CPU, memory and I/O scheduling priority for the
        // current thread as one unit. That is stronger than ThreadPriority alone and is exactly
        // the desired policy for synchronous vendor-driver calls: under game load a sample may
        // arrive late, but the poller must not compete with the game to keep it fresh.
        private const int ThreadModeBackgroundBegin = 0x00010000;
        private const int ThreadModeBackgroundEnd = 0x00020000;

        internal static bool TryBegin()
        {
            return TrySetMode(ThreadModeBackgroundBegin);
        }

        internal static void End()
        {
            TrySetMode(ThreadModeBackgroundEnd);
        }

        private static bool TrySetMode(int mode)
        {
            try
            {
                return SetThreadPriority(GetCurrentThread(), mode);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadPriority(IntPtr thread, int priority);
    }
}
