using CapFrameX.Contracts.Logging;
using CapFrameX.Extensions.NetStandard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace CapFrameX.Data.Logging
{
    public class LogEntryManager : ILogEntryManager
    {
        private bool _showBasicInfo = true;
        private bool _showAdvancedInfo = false;
        private bool _showErrors = true;

        private List<ILogEntry> _logEntryHistory { get; set; }
           = new List<ILogEntry>();

        public ObservableCollection<ILogEntry> LogEntryOutput { get; set; }
           = new ObservableCollection<ILogEntry>();

        public bool ShowBasicInfo 
        {
            get => _showBasicInfo;
            set
            {
                _showBasicInfo = value;
                UpdateFilter();
            }
        }

        public bool ShowAdvancedInfo
        {
            get => _showAdvancedInfo;
            set
            {
                _showAdvancedInfo = value;
                UpdateFilter();
            }
        }

        public bool ShowErrors
        {
            get => _showErrors;
            set
            {
                _showErrors = value;
                UpdateFilter();
            }
        }

        public void AddLogEntry(string message, ELogMessageType messageType, bool isNewLogGroup)
        {
            string newLine = "\n";
            if (!isNewLogGroup || LogEntryOutput.Count == 0)
                newLine = string.Empty;

            Application.Current.Dispatcher.Invoke((() =>
            {
                var logEntry = new LogEntry()
                {
                    MessageType = messageType,
                    MessageInfo = newLine + DateTime.Now.ToString("HH:mm:ss") + $" ( Type: {messageType.GetDescription()} )",
                    Message = message
                };

                _logEntryHistory.Add(logEntry);
                AddLogEntryByFilter(logEntry);
            }));
        }

        public void UpdateFilter()
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                LogEntryOutput.Clear();
                _logEntryHistory.ForEach(AddLogEntryByFilter);
            }));
        }

        private void AddLogEntryByFilter(ILogEntry logEntry)
        {
            switch (logEntry.MessageType)
            {
                case ELogMessageType.Error when ShowErrors:
                    LogEntryOutput.Add(logEntry);
                    break;
                case ELogMessageType.BasicInfo when ShowBasicInfo:
                    LogEntryOutput.Add(logEntry);
                    break;
                case ELogMessageType.AdvancedInfo when ShowAdvancedInfo:
                    LogEntryOutput.Add(logEntry);
                    break;
                default:
                    break;
            }
        }

        public void ClearLog()
        {
            _logEntryHistory?.Clear();
            LogEntryOutput?.Clear();
        }
    }
}
