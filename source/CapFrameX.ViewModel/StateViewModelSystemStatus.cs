using System;

using CapFrameX.Contracts.Data;

namespace CapFrameX.ViewModel
{
    public partial class StateViewModel
    {
        public bool IsResizableBarD3DEnabled => _systemInfo.ResizableBarD3DStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsResizableBarD3DStatusValid => _systemInfo.ResizableBarD3DStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsResizableBarVulkanEnabled => _systemInfo.ResizableBarVulkanStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsResizableBarVulkanStatusValid => _systemInfo.ResizableBarVulkanStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsResizableBarHardwareEnabled => _systemInfo.ResizableBarHardwareStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsResizableBarHardwareStatusValid => _systemInfo.ResizableBarHardwareStatus != ESystemInfoTertiaryStatus.Error;

        public double ResizableBarD3DSize => Math.Round(_systemInfo.PciBarSizeD3D / 1024.0 / 1024);

        public double ResizableBarHardwareSize => Math.Round(_systemInfo.PciBarSizeHardware / 1024.0 / 1024);

        public double ResizableBarVulkanSize => Math.Round(_systemInfo.PciBarSizeVulkan / 1024.0 / 1024);

        public bool IsResizableBarEnabled => (IsResizableBarD3DEnabled || IsResizableBarVulkanEnabled) && IsResizableBarHardwareEnabled;

        public string ResizableBarStatus
        {
            get
            {
                if (IsResizableBarD3DEnabled && IsResizableBarVulkanEnabled) return "On";
                if (IsResizableBarD3DEnabled || IsResizableBarVulkanEnabled) return "Partial";
                return "Off";
            }
        }

        public string ResizableBarStatusColor
        {
            get
            {
                if (IsResizableBarD3DEnabled && IsResizableBarVulkanEnabled) return "LimeGreen";
                if (IsResizableBarD3DEnabled || IsResizableBarVulkanEnabled) return "Orange";
                return "OrangeRed";
            }
        }

        public bool IsResizableBarAnyStatusValid => IsResizableBarD3DStatusValid || IsResizableBarVulkanStatusValid || IsResizableBarHardwareStatusValid;

        public bool IsGameModeEnabled => _systemInfo.GameModeStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsGameModeStatusValid => _systemInfo.GameModeStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsHAGSEnabled => _systemInfo.HardwareAcceleratedGPUSchedulingStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsHAGSStatusValid => _systemInfo.HardwareAcceleratedGPUSchedulingStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsWindowsAnyStatusValid => IsGameModeStatusValid || IsHAGSStatusValid;

		public void UpdateSystemInfoStatus()
        {
            RaisePropertyChanged(nameof(IsResizableBarD3DEnabled));
            RaisePropertyChanged(nameof(IsResizableBarD3DStatusValid));
            RaisePropertyChanged(nameof(IsResizableBarVulkanEnabled));
            RaisePropertyChanged(nameof(IsResizableBarVulkanStatusValid));
            RaisePropertyChanged(nameof(IsResizableBarHardwareEnabled));
            RaisePropertyChanged(nameof(IsResizableBarHardwareStatusValid));
            RaisePropertyChanged(nameof(ResizableBarD3DSize));
            RaisePropertyChanged(nameof(ResizableBarHardwareSize));
            RaisePropertyChanged(nameof(ResizableBarVulkanSize));
            RaisePropertyChanged(nameof(IsResizableBarEnabled));
            RaisePropertyChanged(nameof(ResizableBarStatus));
            RaisePropertyChanged(nameof(ResizableBarStatusColor));
            RaisePropertyChanged(nameof(IsResizableBarAnyStatusValid));
            RaisePropertyChanged(nameof(IsGameModeEnabled));
            RaisePropertyChanged(nameof(IsGameModeStatusValid));
            RaisePropertyChanged(nameof(IsHAGSEnabled));
            RaisePropertyChanged(nameof(IsHAGSStatusValid));
            RaisePropertyChanged(nameof(IsWindowsAnyStatusValid));
		}
    }
}
