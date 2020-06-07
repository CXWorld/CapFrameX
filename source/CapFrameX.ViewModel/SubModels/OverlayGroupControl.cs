using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Windows.Input;

namespace CapFrameX.ViewModel.SubModels
{
    public class OverlayGroupControl : BindableBase
    {
        private readonly OverlayViewModel _overlayViewModel;

        private bool _overlayGroupCaptureItems;
        private bool _overlayGroupSystemInfo;
        private bool _overlayGroupOnlineMetrics;
        private bool _overlayGroupCPULoads;
        private bool _overlayGroupCPUClocks;
        private bool _overlayGroupCPUTemps;
        private bool _overlayGroupCPUPackage;
        private bool _overlayGroupRAMItems;

        private bool _useGroupCaptureItems;
        private bool _useGroupSystemInfo;
        private bool _useGroupOnlineMetrics;
        private bool _useGroupCPULoads;
        private bool _useGroupCPUClocks;
        private bool _useGroupCPUTemps;
        private bool _useGroupCPUPackage;
        private bool _useGroupRAMItems;

        public bool OverlayGroupCaptureItems
        {
            get { return _overlayGroupCaptureItems; }
            set { _overlayGroupCaptureItems = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupSystemInfo
        {
            get { return _overlayGroupSystemInfo; }
            set { _overlayGroupSystemInfo = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupOnlineMetrics
        {
            get { return _overlayGroupOnlineMetrics; }
            set { _overlayGroupOnlineMetrics = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupCPULoads
        {
            get { return _overlayGroupCPULoads; }
            set { _overlayGroupCPULoads = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupCPUClocks
        {
            get { return _overlayGroupCPUClocks; }
            set { _overlayGroupCPUClocks = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupCPUTemps
        {
            get { return _overlayGroupCPUTemps; }
            set { _overlayGroupCPUTemps = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupCPUPackage
        {
            get { return _overlayGroupCPUPackage; }
            set { _overlayGroupCPUPackage = value; RaisePropertyChanged(); }
        }

        public bool OverlayGroupRAMItems
        {
            get { return _overlayGroupRAMItems; }
            set { _overlayGroupRAMItems = value; RaisePropertyChanged(); }
        }

        public bool UseGroupCaptureItems
        {
            get { return _useGroupCaptureItems; }
            set { _useGroupCaptureItems = value; RaisePropertyChanged(); }
        }

        public bool UseGroupSystemInfo
        {
            get { return _useGroupSystemInfo; }
            set { _useGroupSystemInfo = value; RaisePropertyChanged(); }
        }

        public bool UseGroupOnlineMetrics
        {
            get { return _useGroupOnlineMetrics; }
            set { _useGroupOnlineMetrics = value; RaisePropertyChanged(); }
        }

        public bool UseGroupCPULoads
        {
            get { return _useGroupCPULoads; }
            set { _useGroupCPULoads = value; RaisePropertyChanged(); }
        }

        public bool UseGroupCPUClocks
        {
            get { return _useGroupCPUClocks; }
            set { _useGroupCPUClocks = value; RaisePropertyChanged(); }
        }

        public bool UseGroupCPUTemps
        {
            get { return _useGroupCPUTemps; }
            set { _useGroupCPUTemps = value; RaisePropertyChanged(); }
        }

        public bool UseGroupCPUPackage
        {
            get { return _useGroupCPUPackage; }
            set { _useGroupCPUPackage = value; RaisePropertyChanged(); }
        }

        public bool UseGroupRAMItems
        {
            get { return _useGroupRAMItems; }
            set { _useGroupRAMItems = value; RaisePropertyChanged(); }
        }

        public ICommand AcceptOSDGroupSettingsCommand { get; }

        public OverlayGroupControl(OverlayViewModel overlayViewModel)
        {
            _overlayViewModel = overlayViewModel;

            AcceptOSDGroupSettingsCommand = new DelegateCommand( () => OnGroupSettingsChanged());
        }

        private void OnGroupSettingsChanged()
        {
            // manage CX entries
            if (UseGroupCaptureItems)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.Identifier == "CaptureServiceStatus"
                        || item.Identifier == "CaptureTimer"
                        || item.Identifier == "RunHistory"))
                {
                    if(entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupCaptureItems;
                }
            }

            // manage system info entries
            if (UseGroupSystemInfo)
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
                        entry.ShowOnOverlay = OverlayGroupSystemInfo;
                }
            }

            // manage online metrics entries
            if (UseGroupOnlineMetrics)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.Identifier == "OnlineAverage"
                        || item.Identifier == "OnlineP1"
                        || item.Identifier == "OnlineP0dot2"))
                {
                    if (entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupOnlineMetrics;
                }
            }

            // manage CPU load entries
            if (UseGroupCPULoads)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
                {
                    if (entry.Identifier.Contains("load") && entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupCPULoads;
                }
            }

            // manage CPU clocks entries
            if (UseGroupCPUClocks)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
                {
                    if (entry.Identifier.Contains("clock") && entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupCPUClocks;
                }
            }

            // manage CPU temps entries
            if (UseGroupCPUTemps)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
                {
                    if (entry.Identifier.Contains("temperature") && entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupCPUTemps;
                }
            }

            // manage CPU package entries
            if (UseGroupCPUPackage)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.CPU))
                {
                    if (entry.Identifier.Contains("power") && entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupCPUPackage;
                }
            }

            // manage RAM entries
            if (UseGroupRAMItems)
            {
                foreach (var entry in _overlayViewModel.OverlayEntries
                    .Where(item => item.OverlayEntryType == Contracts.Overlay.EOverlayEntryType.RAM))
                {
                    if (entry.ShowOnOverlayIsEnabled)
                        entry.ShowOnOverlay = OverlayGroupRAMItems;
                }
            }
        }
    }
}
