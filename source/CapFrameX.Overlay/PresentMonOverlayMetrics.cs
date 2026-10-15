using System;
using System.Globalization;
using System.Reactive.Linq;
using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.PresentMonInterface;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// Collects frame scalars independently of the native renderer. A single overlay refresh
    /// closes the interval before the resulting snapshot reaches either renderers or the API.
    /// It also resolves the RTSS macro <c>&lt;APP&gt;</c>: with the API the in-game renderer
    /// proved for the target, else with the PresentMon runtime.
    /// </summary>
    public sealed class PresentMonOverlayMetrics : IOverlayFrameMetrics
    {
        private readonly object _gate = new object();
        private readonly IObservable<string[]> _frames;
        private readonly IDisposable _processSubscription;
        private readonly IDisposable _captureSubscription;
        private readonly IDisposable _hookStatusSubscription;
        private IDisposable _frameSubscription;
        private bool _enabled;
        private bool _disposed;
        private bool _captureRunning = true;
        private int _targetPid;
        private long _subscriptionGeneration;
        private double _frametimeSum;
        private long _frametimeCount;
        private double _displayTimeSum;
        private long _displayTimeCount;
        private double _framerate;
        private double _frametime;
        private double _displayTime;
        private string _runtime;
        // PresentMon only knows the presentation runtime (DXGI, D3D9, Other). The in-game renderer
        // knows the API itself, as RTSS does: the hook proves the device type of the swapchain it
        // presents into, the Vulkan layer settles it by being loaded.
        private HookOverlayStatus _hookStatus;
        private string _renderApi;

        public PresentMonOverlayMetrics(ICaptureService captureService, IRTSSService processService,
            IHookOverlayStatusService hookOverlayStatusService = null)
            : this((captureService ?? throw new ArgumentNullException(nameof(captureService))).FrameDataStream,
                  (processService ?? throw new ArgumentNullException(nameof(processService))).ProcessIdStream,
                  captureService.CaptureServiceRunningStream, hookOverlayStatusService?.StatusStream)
        {
        }

        internal PresentMonOverlayMetrics(IObservable<string[]> frames, IObservable<int> processIds,
            IObservable<bool> captureRunning = null, IObservable<HookOverlayStatus> hookStatuses = null)
        {
            _frames = frames ?? throw new ArgumentNullException(nameof(frames));
            if (processIds == null)
                throw new ArgumentNullException(nameof(processIds));

            _processSubscription = processIds.Subscribe(pid =>
            {
                lock (_gate)
                {
                    int targetPid = Math.Max(0, pid);
                    if (_targetPid != targetPid)
                    {
                        _targetPid = targetPid;
                        ResetLocked();
                        // The status stream runs independently of the PID stream: a status for
                        // the new target may already have arrived.
                        _renderApi = null;
                        TrackRenderApiLocked();
                    }
                }
            });
            _captureSubscription = captureRunning?.Subscribe(running =>
            {
                lock (_gate)
                {
                    _captureRunning = running;
                    if (!running)
                        ResetLocked();
                }
            });
            _hookStatusSubscription = hookStatuses?.Subscribe(status =>
            {
                lock (_gate)
                {
                    _hookStatus = status;
                    TrackRenderApiLocked();
                }
            });
        }

        private void TrackRenderApiLocked()
        {
            var status = _hookStatus;
            if (status == null || status.State == EHookOverlayStatus.Disabled)
            {
                // The in-game overlay is not selected: hook-free and the remote API keep the
                // PresentMon runtime, and RTSS resolves the macro itself.
                _renderApi = null;
                return;
            }

            // Statuses between injection steps, or while the overlay is hidden, carry no API. The
            // last proven one stays for the process: each label flip renames the group, and the
            // renderer rebuilds its scene for it.
            string api = status.RenderApi?.Trim();
            if (_targetPid > 0 && status.ProcessId == _targetPid && !string.IsNullOrEmpty(api))
                _renderApi = api;
        }

        public void SetEnabled(bool enabled)
        {
            lock (_gate)
            {
                if (_disposed || _enabled == enabled)
                    return;

                _enabled = enabled;
                long generation = ++_subscriptionGeneration;
                ResetLocked();
                if (enabled)
                {
                    _frameSubscription = _frames.Subscribe(row => OnFrame(row, generation));
                }
                else
                {
                    _frameSubscription?.Dispose();
                    _frameSubscription = null;
                }
            }
        }

        private void OnFrame(string[] row, long generation)
        {
            if (row == null || row.Length <= PresentMonCaptureService.ProcessID_INDEX ||
                !int.TryParse(row[PresentMonCaptureService.ProcessID_INDEX], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int pid))
                return;

            lock (_gate)
            {
                // Target checks and accumulation share the reset lock: an in-flight row cannot
                // repopulate the previous process's metrics after a PID switch or disable.
                if (_disposed || !_enabled || !_captureRunning || generation != _subscriptionGeneration ||
                    _targetPid <= 0 || pid != _targetPid)
                    return;

                if (row.Length > PresentMonCaptureService.PresentRuntime_INDEX)
                {
                    string runtime = row[PresentMonCaptureService.PresentRuntime_INDEX]?.Trim();
                    if (!string.IsNullOrEmpty(runtime) && runtime != "<error>")
                        _runtime = runtime;
                }

                if (TryReadTime(row, PresentMonCaptureService.MsBetweenPresents_INDEX, out double frametime))
                {
                    _frametimeSum += frametime;
                    _frametimeCount++;
                }
                // Dropped frames have a zero display interval and do not enter the display mean.
                if (TryReadTime(row, PresentMonCaptureService.MsBetweenDisplayChange_INDEX, out double displayTime))
                {
                    _displayTimeSum += displayTime;
                    _displayTimeCount++;
                }
            }
        }

        private static bool TryReadTime(string[] row, int index, out double value)
        {
            value = 0;
            return row.Length > index && double.TryParse(row[index], NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) && value > 0 && value < 10000;
        }

        public IOverlayEntry[] ApplySnapshot(IOverlayEntry[] entries)
        {
            lock (_gate)
            {
                if (!_enabled || _disposed || entries == null)
                    return entries;

                // Retain the last completed mean between PresentMon delivery waves.
                if (_frametimeCount > 0)
                {
                    _frametime = _frametimeSum / _frametimeCount;
                    _framerate = 1000.0 / _frametime;
                    _frametimeSum = 0;
                    _frametimeCount = 0;
                }
                if (_displayTimeCount > 0)
                {
                    _displayTime = _displayTimeSum / _displayTimeCount;
                    _displayTimeSum = 0;
                    _displayTimeCount = 0;
                }

                IOverlayEntry[] result = entries;
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    if (entry == null)
                        continue;

                    bool isFrameValue = entry.Identifier == "Framerate" || entry.Identifier == "Frametime" ||
                        entry.Identifier == "DisplayTime";
                    bool resolveGroup = entry.GroupName?.IndexOf("<APP>", StringComparison.Ordinal) >= 0;
                    if (!isFrameValue && !resolveGroup)
                        continue;

                    if (ReferenceEquals(result, entries))
                        result = (IOverlayEntry[])entries.Clone();
                    var copy = entry.Clone();
                    if (isFrameValue)
                    {
                        copy.IsNumeric = true;
                        copy.Value = entry.Identifier == "Framerate" ? _framerate
                            : entry.Identifier == "Frametime" ? _frametime : _displayTime;
                    }
                    if (resolveGroup)
                    {
                        string runtime = _renderApi ?? _runtime ?? CxLang.Instance.TranslateOverlay("Performance");
                        copy.GroupName = entry.GroupName.Replace("<APP>", runtime);
                    }
                    result[i] = copy;
                }
                return result;
            }
        }

        private void ResetLocked()
        {
            _frametimeSum = 0;
            _frametimeCount = 0;
            _displayTimeSum = 0;
            _displayTimeCount = 0;
            _framerate = 0;
            _frametime = 0;
            _displayTime = 0;
            _runtime = null;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _enabled = false;
                ++_subscriptionGeneration;
                _frameSubscription?.Dispose();
                _frameSubscription = null;
                ResetLocked();
            }
            _processSubscription.Dispose();
            _captureSubscription?.Dispose();
            _hookStatusSubscription?.Dispose();
        }
    }
}
