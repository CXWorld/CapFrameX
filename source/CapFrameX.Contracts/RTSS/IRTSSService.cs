using CapFrameX.Contracts.Overlay;
using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.RTSS
{
    public interface IRTSSService
    {
        ISubject<int> ProcessIdStream { get; }
        bool IsRTSSInstalled();
        string GetApiInfo(int processId);
        Tuple<double, double> GetCurrentFramerate(int processId);
        Task CheckRTSSRunningAndRefresh();
        Task CheckRTSSRunning();
        void Refresh();
        void ClearOSD();
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
        void SetShowRunHistory(bool showRunHistory);
        void SetOSDCustomPosition(bool active);
        void SetOverlayPosition(int x, int y);
    }
}
