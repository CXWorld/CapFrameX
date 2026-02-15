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

        public string HardwareName { get; set; }

        public bool UseForLogging
        {
            get => _useForLogging;
            set
            {
                _useForLogging = value;
                var stableId = SensorIdentifierHelper.BuildStableIdentifier(this);
                UpdateLogState?.Invoke(Identifier, stableId, value);
                RaisePropertyChanged();
            }
        }

        public Action<string, string, bool> UpdateLogState { get; set; }
    }
}
