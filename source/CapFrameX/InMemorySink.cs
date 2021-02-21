using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX
{
    public class InMemorySink : ILogEventSink, IDisposable
    {
        private static readonly List<LogEvent> _logEvents = new List<LogEvent>();

        public InMemorySink()
        {
        }

        public static IEnumerable<LogEvent> LogEvents => _logEvents.AsReadOnly();

        public void Emit(LogEvent logEvent)
        {
            _logEvents.Add(logEvent);
        }

        public void Dispose()
        {
            _logEvents.Clear();
        }
    }
}
