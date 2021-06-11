using System.Collections.ObjectModel;

namespace CapFrameX.Contracts.Logging
{
    public interface ILogEntryManager
    {
        ObservableCollection<ILogEntry> LogEntryOutput { get; }

        void AddLogEntry(string message, ELogMessageType messageType);

        void UpdateFilter();
    }
}
