using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;

namespace CapFrameX
{
    public class InMemorySink : ILogEventSink, IDisposable
    {
        public const int MaxRetainedEvents = 2000;
        private static readonly object _gate = new object();
        private static readonly Queue<LogEvent> _logEvents = new Queue<LogEvent>();

        public InMemorySink() { }

        public static IEnumerable<LogEvent> LogEvents
        {
            get
            {
                lock (_gate)
                {
                    return _logEvents.ToArray();
                }
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_gate)
            {
                if (_logEvents.Count == MaxRetainedEvents)
                    _logEvents.Dequeue();

                _logEvents.Enqueue(logEvent);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _logEvents.Clear();
            }
        }
    }
}
