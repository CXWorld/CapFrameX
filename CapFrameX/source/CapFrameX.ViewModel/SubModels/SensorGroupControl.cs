using Prism.Commands;
using Prism.Mvvm;
using System.Linq;
using System.Windows.Input;

namespace CapFrameX.ViewModel.SubModels
{
    public class SensorGroupControl : BindableBase
    {
        private readonly SensorViewModel _sensorViewModel;

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

            CheckCpuBasics = new DelegateCommand(() => ManageCpuBasicEntries(true));
            CheckCpuThreadLoads = new DelegateCommand(() => ManageCpuThreadLoadEntries(true));
            CheckCpuCoreClocks = new DelegateCommand(() => ManageCpuCoreClocksEntries(true));
            CheckGpuBasics = new DelegateCommand(() => ManageGpuBasicEntries(true));
            CheckAll = new DelegateCommand(() => ManageAllEntries(true));

            UncheckCpuBasics = new DelegateCommand(() => ManageCpuBasicEntries(false));
            UncheckCpuThreadLoads = new DelegateCommand(() => ManageCpuThreadLoadEntries(false));
            UncheckCpuCoreClocks = new DelegateCommand(() => ManageCpuCoreClocksEntries(false));
            UncheckGpuBasics = new DelegateCommand(() => ManageGpuBasicEntries(false));
            UncheckAll = new DelegateCommand(() => ManageAllEntries(false));
        }

        private void ManageCpuBasicEntries(bool logEntry)
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                   .Where(item => item.Name == "CPU Total"
                       || item.Name == "CPU Max"
                       || item.Name == "CPU Max Clock"
                       || item.Name == "CPU Package"))
            {
                entry.UseForLogging = logEntry;
            }
        }

        private void ManageCpuThreadLoadEntries(bool logEntry)
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                   .Where(item => item.SensorType == "Load" && item.Name.Contains("CPU Core")))
            {
                entry.UseForLogging = logEntry;
            }
        }

        private void ManageCpuCoreClocksEntries(bool logEntry)
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                   .Where(item => item.SensorType == "Clock" && item.Name.Contains("CPU Core")))
            {
                entry.UseForLogging = logEntry;
            }
        }

        private void ManageGpuBasicEntries(bool logEntry)
        {
            foreach (var entry in _sensorViewModel.SensorEntries
                 .Where(item => item.Name.Contains("GPU")))
            {
                if (entry.Name.Contains("GPU Core") || entry.Name == "GPU Memory Dedicated" || entry.SensorType == "Power")
                    entry.UseForLogging = logEntry;
            }
        }

        private void ManageAllEntries(bool logEntry)
        {
            foreach (var entry in _sensorViewModel.SensorEntries)
            {
                entry.UseForLogging = logEntry;
            }
        }
    }
}