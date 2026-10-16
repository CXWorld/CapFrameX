using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    /// <summary>
    /// Rewrites record files off the UI thread: a metadata edit parses and serializes the whole
    /// record. The writes run one after the other in the order they were queued, so two edits of
    /// a record never write it at the same time. A rewrite truncates the file first, so the app
    /// waits for the queue before it exits.
    /// </summary>
    public sealed class RecordWriteQueue
    {
        private readonly ILogger<RecordWriteQueue> _logger;
        private readonly object _sync = new object();
        private Task _last = Task.CompletedTask;

        public RecordWriteQueue(ILogger<RecordWriteQueue> logger)
        {
            _logger = logger;
        }

        /// <summary>Completes when the write has run. A failing write is logged, never thrown.</summary>
        public Task Enqueue(Action write)
        {
            lock (_sync)
            {
                _last = _last.ContinueWith(_ => Run(write), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default);
                return _last;
            }
        }

        /// <summary>False if writes are still running after <paramref name="timeout"/>.</summary>
        public bool WaitForPendingWrites(TimeSpan timeout)
        {
            Task last;
            lock (_sync)
                last = _last;
            return last.Wait(timeout);
        }

        private void Run(Action write)
        {
            try
            {
                write();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while writing a record file");
            }
        }
    }
}
