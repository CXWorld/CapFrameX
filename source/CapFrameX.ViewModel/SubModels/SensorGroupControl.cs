using Prism.Commands;
using Prism.Mvvm;
using System.Linq;
using System.Windows.Input;

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
                ManageGpuBasicEntries();
            }
        }

        public bool SensorGroupAll
        {
            get { return _sensorGroupAll; }
            set
            {
                _sensorGroupAll = value;
                RaisePropertyChanged();
                ManageAllEntries();
            }
        }

        public ICommand CheckCpuBasics { get; }
        public ICommand CheckCpuThreadLoads { get; }
        public ICommand CheckCpuCoreClocks { get; }
        public ICommand CheckGpuBasics { get; }
        public ICommand CheckAll { get; }
        public ICommand UncheckCpuBasics { get; }
        public ICommand UncheckCpuThreadLoads { get; }
        public ICommand UncheckCpuCoreClocks { get; }
        public ICommand UncheckGpuBasics { get; }
        public ICommand UncheckAll { get; }

        public SensorGroupControl(SensorViewModel sensorViewModel)
        {
            _sensorViewModel = sensorViewModel;

            CheckCpuBasics = new DelegateCommand(() => SensorGroupCpuBasics = true);
            CheckCpuThreadLoads = new DelegateCommand(() => SensorGroupCpuThreadLoads = true);
            CheckCpuCoreClocks = new DelegateCommand(() => SensorGroupCpuCoreClocks = true);
            CheckGpuBasics = new DelegateCommand(() => SensorGroupGpuBasics = true);
            CheckAll = new DelegateCommand(() => SensorGroupAll = true);
            UncheckCpuBasics = new DelegateCommand(() => SensorGroupCpuBasics = false);
            UncheckCpuThreadLoads = new DelegateCommand(() => SensorGroupCpuThreadLoads = false);
            UncheckCpuCoreClocks = new DelegateCommand(() => SensorGroupCpuCoreClocks = false);
            UncheckGpuBasics = new DelegateCommand(() => SensorGroupGpuBasics = false);
            UncheckAll = new DelegateCommand(() => SensorGroupAll = false);
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