using System.Collections.ObjectModel;

namespace CapFrameX.Contracts.Logging
{
    public interface ILogEntryManager
    {
        bool ShowBasicInfo { get; set; }

        bool ShowAdvancedInfo { get; set; }

        bool ShowErrors { get; set; }

        ObservableCollection<ILogEntry> LogEntryOutput { get; }

        void AddLogEntry(string message, ELogMessageType messageType, bool isNewLogGroup);

        void UpdateFilter();

        void ClearLog();
    }
}
