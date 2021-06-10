using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace CapFrameX.ViewModel.LoggerEntry
{
    public class LogEntry : PropertyChangedDispatcherBase
    {
        public string MessageInfo { get; set; }
        public string MessageType { get; set; }
        public int PriorityLevel { get; set; }
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
        public List<LogEntry> Contents { get; set; }
    }
}
