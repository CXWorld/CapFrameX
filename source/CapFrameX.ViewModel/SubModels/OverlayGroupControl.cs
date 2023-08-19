using Prism.Commands;
using Prism.Mvvm;
using System.Linq;
using System.Windows.Input;

namespace CapFrameX.ViewModel.SubModels
{
    public class OverlayGroupControl : BindableBase
    {
        private readonly OverlayViewModel _overlayViewModel;

        public ICommand CheckCaptureItems { get; }
        public ICommand CheckSystemInfo { get; }
        public ICommand CheckOnlineMetrics { get; }
        public ICommand CheckGpuBasics { get; }
        public ICommand CheckCpuLoads { get; }
        public ICommand CheckCpuClocks { get; }
        public ICommand CheckCpuPowers { get; }
        public ICommand CheckCpuTemps { get; }
        public ICommand CheckCpuVoltages { get; }
        public ICommand CheckRamItems { get; }
        public ICommand UncheckCaptureItems { get; }
        public ICommand UncheckSystemInfo { get; }
        public ICommand UncheckOnlineMetrics { get; }
        public ICommand UncheckGpuBasics { get; }
        public ICommand UncheckCpuLoads { get; }
        public ICommand UncheckCpuClocks { get; }
        public ICommand UncheckCpuPowers { get; }
        public ICommand UncheckCpuTemps { get; }
        public ICommand UncheckCpuVoltages { get; }
        public ICommand UncheckRamItems { get; }

        public OverlayGroupControl(OverlayViewModel overlayViewModel)
        {
            _overlayViewModel = overlayViewModel;

            CheckCaptureItems = new DelegateCommand(() => ManageCXEntries(true));
            CheckSystemInfo = new DelegateCommand(() => ManageSystemInfoEntries(true));
            CheckOnlineMetrics = new DelegateCommand(() => ManageOnlineMetricEntries(true));
            CheckGpuBasics = new DelegateCommand(() => ManageGpuBasicEntries(true));
            CheckCpuLoads = new DelegateCommand(() => ManageCPULoadEntries(true));
            CheckCpuClocks = new DelegateCommand(() => ManageCPUClockEntries(true));
            CheckCpuPowers = new DelegateCommand(() => ManageCPUPowerEntries(true));
            CheckCpuTemps = new DelegateCommand(() => ManageCPUTemperatureEntries(true));
            CheckCpuVoltages = new DelegateCommand(() => ManageCPUVoltageEntries(true));
            CheckRamItems = new DelegateCommand(() => ManageRAMEntries(true));

            UncheckCaptureItems = new DelegateCommand(() => ManageCXEntries(false));
            UncheckSystemInfo = new DelegateCommand(() => ManageSystemInfoEntries(false));
            UncheckOnlineMetrics = new DelegateCommand(() => ManageOnlineMetricEntries(false));
            UncheckGpuBasics = new DelegateCommand(() => ManageGpuBasicEntries(false));
            UncheckCpuLoads = new DelegateCommand(() => ManageCPULoadEntries(false));
            UncheckCpuClocks = new DelegateCommand(() => ManageCPUClockEntries(false));
            UncheckCpuPowers = new DelegateCommand(() => ManageCPUPowerEntries(false));
            UncheckCpuTemps = new DelegateCommand(() => ManageCPUTemperatureEntries(false));
            UncheckCpuVoltages = new DelegateCommand(() => ManageCPUVoltageEntries(false));
            UncheckRamItems = new DelegateCommand(() => ManageRAMEntries(false));
        }

        private void ManageCXEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.Identifier == "CaptureServiceStatus"
                       || item.Identifier == "CaptureTimer"
                       || item.Identifier == "RunHistory"))
            {
                if (entry.ShowOnOverlayIsEnabled)
                {
                    entry.ShowOnOverlay = showEntry;
                }
            }
        }

        private void ManageSystemInfoEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.Identifier == "CustomCPU"
                       || item.Identifier == "CustomGPU"
                       || item.Identifier == "Mainboard"
                       || item.Identifier == "CustomRAM"
                       || item.Identifier == "OS"
                       || item.Identifier == "GPUDriver"))
            {
                if (entry.ShowOnOverlayIsEnabled)
                {
                    entry.ShowOnOverlay = showEntry;
                }
            }
        }

        private void ManageOnlineMetricEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.Identifier == "OnlineAverage"
                       || item.Identifier == "OnlineP1"
                       || item.Identifier == "OnlineP0dot2"
					   || item.Identifier == "Online1PercentLow"
					   || item.Identifier == "Online0dot2PercentLow"
					   || item.Identifier == "OnlineGpuActiveAverage"
					   || item.Identifier == "OnlineGpuActiveP1"
					   || item.Identifier == "OnlineGpuActive1PercentLow"
					   || item.Identifier == "OnlineApplicationLatency"
                       || item.Identifier == "OnlineStutteringPercentage"
                       || item.Identifier == "PmdGpuPowerCurrent"
                       || item.Identifier == "PmdCpuPowerCurrent"
                       || item.Identifier == "PmdSystemPowerCurrent"))
            {
                if (entry.ShowOnOverlayIsEnabled)
                {
                    entry.ShowOnOverlay = showEntry;
                }
            }
        }

        private void ManageGpuBasicEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                 .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.GPU))
            {
                if (entry.Description.Contains("GPU Core") && entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }

        private void ManageCPULoadEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
            {
                if (entry.Identifier.Contains("load") && entry.Description.Contains("CPU Core #")
                    && entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }

        private void ManageCPUClockEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
            {
                if (entry.Identifier.Contains("clock") && entry.Description.Contains("CPU Core #")
                    && entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }

        private void ManageCPUPowerEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
            {
                if (entry.Identifier.Contains("power") && entry.Description.Contains("CPU Core #")
                    && entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }

        private void ManageCPUTemperatureEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
            {
                if (entry.Identifier.Contains("temperature") && entry.Description.Contains("CPU Core #")
                    && entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }

        private void ManageCPUVoltageEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                  .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
            {
                if (entry.Identifier.Contains("voltage") && entry.Description.Contains("CPU Core #")
                    && entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }

        private void ManageRAMEntries(bool showEntry)
        {
            foreach (var entry in _overlayViewModel.OverlayEntries
                   .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.RAM))
            {
                if (entry.ShowOnOverlayIsEnabled)
                    entry.ShowOnOverlay = showEntry;
            }
        }
    }
}
