using CapFrameX.Contracts.Overlay;
using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.RTSS
{
    public interface IRTSSService
    {
        ISubject<uint> ProcessIdStream { get; }
        bool IsRTSSInstalled();
        bool IsProcessDetected(uint processId);
        string GetApiInfo(uint processId);
        Tuple<double, double> GetCurrentFramerate(uint processId);
        Tuple<double, double> GetCurrentFramerateFromForegroundWindow();
        Task CheckRTSSRunningAndRefresh();
        Task CheckRTSSRunning();
        void ClearOSD();
        bool IsOSDLocked();
        void ReleaseOSD();
        void SetOverlayEntries(IOverlayEntry[] entries);
        void SetFormatVariables(string variables);
        void SetOverlayEntry(IOverlayEntry entry);
        void SetIsCaptureTimerActive(bool active);
        void SetRunHistoryOutlierFlags(bool[] flags);
        void SetRunHistory(string[] history);
        void SetRunHistoryAggregation(string aggregation);
        void OnOSDOn();
        void OnOSDOff();
        void OnOSDToggle();
        void CloseHandles();
        void SetShowRunHistory(bool showRunHistory);
    }
}
