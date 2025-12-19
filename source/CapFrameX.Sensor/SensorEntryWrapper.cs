using CapFrameX.Contracts.Sensor;
using Prism.Mvvm;
using System;

namespace CapFrameX.Sensor
{
    public class SensorEntryWrapper : BindableBase, ISensorEntry
    {
        private bool _useForLogging;

        public string Identifier { get; set; }

        public string SortKey { get; set; }

        public string Name { get; set; }

        public object Value { get; set; }

        public string HardwareType { get; set; }

        public string SensorType { get; set; }

        public bool IsPresentationDefault { get; set; }

        public bool UseForLogging
        {
            get => _useForLogging;
            set
            {
                _useForLogging = value;
                UpdateLogState?.Invoke(Identifier, value);
                RaisePropertyChanged();
            }
        }

        public Action<string, bool> UpdateLogState { get; set; }
    }
}
