using CapFrameX.Contracts.Logging;
using CapFrameX.Extensions.NetStandard;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace CapFrameX.Data.Logging
{
    public class LogEntryManager : ILogEntryManager
    {
        private ObservableCollection<ILogEntry> _logEntryHistory { get; set; }
           = new ObservableCollection<ILogEntry>();

        public ObservableCollection<ILogEntry> LogEntryOutput { get; set; }
           = new ObservableCollection<ILogEntry>();

        public bool ShowBasicInfo { get; set; } = true;

        public bool ShowAdvancedInfo { get; set; } = true;

        public bool ShowErrors { get; set; } = true;

        public void AddLogEntry(string message, ELogMessageType messageType)
        {
            void Add(ObservableCollection<ILogEntry> collection)
            {
                collection.Add(new LogEntry()
                {
                    MessageType = messageType,
                    MessageInfo = DateTime.Now.ToString("HH:mm:ss") + $" ( Type: {messageType.GetDescription()} )",
                    Message = message
                });
            }

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
            {
                Add(_logEntryHistory);

                switch (messageType)
                {
                    case ELogMessageType.Error when ShowErrors:
                        Add(LogEntryOutput);
                        break;
                    case ELogMessageType.BasicInfo when ShowBasicInfo:
                        Add(LogEntryOutput);
                        break;
                    case ELogMessageType.AdvancedInfo when ShowAdvancedInfo: 
                        Add(LogEntryOutput);
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
