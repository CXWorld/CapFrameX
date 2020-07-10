using CapFrameX.Contracts.Overlay;
using System;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.RTSS
{
    public interface IRTSSService
    {
        ISubject<uint> ProcessIdStream { get; }
        bool IsRTSSInstalled();
        string GetApiInfo(uint processId);
        Tuple<double, double> GetCurrentFramerate(uint processId);
        void CheckRTSSRunningAndRefresh();
        void ResetOSD();
        void ReleaseOSD();
        void SetOverlayEntries(IOverlayEntry[] entries);
        void SetOverlayEntry(IOverlayEntry entry);
        void SetIsCaptureTimerActive(bool active);
        void SetRunHistoryOutlierFlags(bool[] flags);
        void SetRunHistory(string[] history);
        void SetRunHistoryAggregation(string aggregation);
    }
}
