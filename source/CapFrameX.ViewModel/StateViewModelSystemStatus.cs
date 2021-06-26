using CapFrameX.Contracts.Data;

namespace CapFrameX.ViewModel
{
    public partial class StateViewModel
    {
        public bool IsResizableBarSoftwareEnabled => _systemInfo.ResizableBarSoftwareStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsResizableBarSoftwareStatusValid => _systemInfo.ResizableBarSoftwareStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsResizableBarHardwareEnabled => _systemInfo.ResizableBarHardwareStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsResizableBarHardwareStatusValid => _systemInfo.ResizableBarHardwareStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsResizableBarEnabled => IsResizableBarSoftwareEnabled && IsResizableBarHardwareEnabled;

        public bool IsResizableBarAnyStatusValid => IsResizableBarSoftwareStatusValid || IsResizableBarHardwareStatusValid;

        public bool IsGameModeEnabled => _systemInfo.GameModeStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsGameModeStatusValid => _systemInfo.GameModeStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsHAGSEnabled => _systemInfo.HardwareAcceleratedGPUSchedulingStatus == ESystemInfoTertiaryStatus.Enabled;

        public bool IsHAGSStatusValid => _systemInfo.HardwareAcceleratedGPUSchedulingStatus != ESystemInfoTertiaryStatus.Error;

        public bool IsWindowsAnyStatusValid => IsGameModeStatusValid || IsHAGSStatusValid;


        public void UpdateSystemInfoStatus()
        {
            RaisePropertyChanged(nameof(IsResizableBarEnabled));
            RaisePropertyChanged(nameof(IsGameModeEnabled));
            RaisePropertyChanged(nameof(IsHAGSEnabled));
        }
    }
}
