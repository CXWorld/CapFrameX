using CapFrameX.Contracts.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace CapFrameX.Data.Logging
{
    public class LogEntry : PropertyChangedDispatcherBase, ILogEntry
    {
        public string MessageInfo { get; set; }
        public ELogMessageType MessageType { get; set; }
        public string Message { get; set; }
    }

    public class PropertyChangedDispatcherBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }));
        }
    }

    public class CollapsibleLogEntry : LogEntry
    {
        public List<ILogEntry> Contents { get; set; }
    }
}
