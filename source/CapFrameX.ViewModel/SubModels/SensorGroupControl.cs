using Prism.Mvvm;
using System.Linq;

namespace CapFrameX.ViewModel.SubModels
{
    public class SensorGroupControl : BindableBase
    {
        private readonly SensorViewModel _sensorViewModel;

        private bool _sensorGroupCpuBasics;
        private bool _sensorGroupCpuThreadLoads;
        private bool _sensorGroupCpuCoreClocks;
        private bool _sensorGroupGpuBasics;
        private bool _sensorGroupAll;

        public bool SensorGroupCpuBasics
        {
            get { return _sensorGroupCpuBasics; }
            set
            {
                _sensorGroupCpuBasics = value;
                RaisePropertyChanged();
                if (value && SensorGroupAll)
                    return;
                RaisePropertyChanged(nameof(SensorGroupAll));
                ManageCpuBasicEntries();
            }
        }

        public bool SensorGroupCpuThreadLoads
        {
            get { return _sensorGroupCpuThreadLoads; }
            set
            {
                _sensorGroupCpuThreadLoads = value;
                RaisePropertyChanged();
                if (value && SensorGroupAll)
                    return;
                RaisePropertyChanged(nameof(SensorGroupAll));
                ManageCpuThreadLoadEntries();
            }
        }

        public bool SensorGroupCpuCoreClocks
        {
            get { return _sensorGroupCpuCoreClocks; }
            set
            {
                _sensorGroupCpuCoreClocks = value;
                RaisePropertyChanged();
                if (value && SensorGroupAll)
                    return;
                RaisePropertyChanged(nameof(SensorGroupAll));
                ManageCpuCoreClocksEntries();
            }
        }

        public bool SensorGroupGpuBasics
        {
            get { return _sensorGroupGpuBasics; }
            set
            {
                _sensorGroupGpuBasics = value;
                RaisePropertyChanged();
                if (value && SensorGroupAll)
                    return;
                RaisePropertyChanged(nameof(SensorGroupAll));
                ManageGpuBasicEntries();
            }
        }

        public bool SensorGroupAll
        {
            get { return (SensorGroupCpuBasics && SensorGroupCpuCoreClocks && SensorGroupCpuThreadLoads && SensorGroupGpuBasics); }
            set
            {
                _sensorGroupAll = value;
                SensorGroupCpuBasics = value;
                SensorGroupCpuThreadLoads = value;
                SensorGroupCpuCoreClocks = value;
                SensorGroupGpuBasics = value;
                RaisePropertyChanged();
                ManageAllEntries();
            }
        }

        public SensorGroupControl(SensorViewModel sensorViewModel)
        {
            _sensorViewModel = sensorViewModel;
        }

        private void ManageCpuBasicEntries()
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                   .Where(item => item.Name == "CPU Total"
                       || item.Name == "CPU Max"
                       || item.Name == "CPU Max Clock"
                       || item.Name == "CPU Package"))
            {
                entry.UseForLogging = SensorGroupCpuBasics;
            }
        }

        private void ManageCpuThreadLoadEntries()
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                   .Where(item => item.SensorType == "Load" && item.Name.Contains("CPU Core")))
            {
                entry.UseForLogging = SensorGroupCpuThreadLoads;
            }
        }

        private void ManageCpuCoreClocksEntries()
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                   .Where(item => item.SensorType == "Clock" && item.Name.Contains("CPU Core")))
            {
                entry.UseForLogging = SensorGroupCpuCoreClocks;
            }
        }

        private void ManageGpuBasicEntries()
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                 .Where(item => item.Name.Contains("GPU")))
            {
                if (entry.Name.Contains("GPU Core") || entry.Name == "GPU Memory Dedicated" || entry.SensorType == "Power")
                    entry.UseForLogging = SensorGroupGpuBasics;
            }
        }

        private void ManageAllEntries()
        {
            foreach (var entry in _sensorViewModel.SensorEntries)
            {
                entry.UseForLogging = SensorGroupAll;
            }
        }
    }
}