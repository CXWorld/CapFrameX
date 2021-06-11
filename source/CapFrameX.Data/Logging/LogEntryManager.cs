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
        private List<ILogEntry> _logEntryHistory { get; set; }
           = new List<ILogEntry>();

        public ObservableCollection<ILogEntry> LogEntryOutput { get; set; }
           = new ObservableCollection<ILogEntry>();

        public bool ShowBasicInfo { get; set; } = true;

        public bool ShowAdvancedInfo { get; set; } = true;

        public bool ShowErrors { get; set; } = true;

        public void AddLogEntry(string message, ELogMessageType messageType)
        {
            var logEntry = new LogEntry()
            {
                MessageType = messageType,
                MessageInfo = DateTime.Now.ToString("HH:mm:ss") + $" ( Type: {messageType.GetDescription()} )",
                Message = message
            };

            _logEntryHistory.Add(logEntry);

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                switch (messageType)
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
            }));
        }

        public void UpdateFilter()
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                LogEntryOutput = new ObservableCollection<ILogEntry>();

                foreach (var entry in _logEntryHistory)
                {
                    switch (entry.MessageType)
                    {
                        case ELogMessageType.Error when ShowErrors:
                            LogEntryOutput.Add(entry);
                            break;
                        case ELogMessageType.BasicInfo when ShowBasicInfo:
                            LogEntryOutput.Add(entry);
                            break;
                        case ELogMessageType.AdvancedInfo when ShowAdvancedInfo:
                            LogEntryOutput.Add(entry);
                            break;
                        default:
                            break;
                    }
                }               
            }));
        }
    }
}
