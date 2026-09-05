using System;
using System.Diagnostics;
using System.Globalization;
using System.Reactive.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Latency;
using Prism.Mvvm;

namespace CapFrameX.ViewModel
{
    public sealed class AmdFlmViewModel : BindableBase
    {
        private readonly IAppConfiguration _configuration;
        private AmdFlmSettings _settings;
        private AmdFlmStatus _status;
        private AmdFlmSample? _lastSample;

        public string[] CaptureModes { get; } =
        {
            "AMF (DirectX 12, includes Vulkan)",
            "AMF (DirectX 11 compatibility)",
            "Desktop capture (DXGI, windowed / borderless)"
        };

        public int CaptureMode { get => _settings.CaptureMode; set => Update(captureMode: value); }
        public int CaptureOutputIndex { get => _settings.CaptureOutputIndex; set => Update(output: value); }
        public double LeftPercent { get => _settings.StartX * 100; set => Update(x: value / 100); }
        public double TopPercent { get => _settings.StartY * 100; set => Update(y: value / 100); }
        public double WidthPercent { get => _settings.Width * 100; set => Update(width: value / 100); }
        public double HeightPercent { get => _settings.Height * 100; set => Update(height: value / 100); }
        public double ThresholdCoefficient { get => _settings.ThresholdCoefficient; set => Update(threshold: value); }
        public double PreviewLeft => _settings.StartX * 320;
        public double PreviewTop => _settings.StartY * 180;
        public double PreviewWidth => _settings.Width * 320;
        public double PreviewHeight => _settings.Height * 180;
        public string StatusMessage => _status?.Message ?? "AMD FLM is disabled.";
        public string DiagnosticCounts => _status == null ? string.Empty :
            $"Captured frames: {_status.Frames}   Clicks: {_status.Clicks}   Rejected: {_status.RejectedClicks}   No response: {_status.Timeouts}";

        public string LastMeasurement
        {
            get
            {
                if (!_lastSample.HasValue)
                    return "Last click: no measurement yet";
                double age = Math.Max(0, (Stopwatch.GetTimestamp() - _lastSample.Value.FrameQpc) / (double)Stopwatch.Frequency);
                return string.Format(CultureInfo.InvariantCulture, "Last click: {0:F1} ms ({1:F0} s ago)", _lastSample.Value.LatencyMs, age);
            }
        }

        public AmdFlmViewModel(IAppConfiguration configuration, IAmdFlmService service)
        {
            _configuration = configuration;
            _settings = AmdFlmSettings.FromConfiguration(configuration);
            _status = service.Status;
            // This settings view model shares the application's lifetime, as does FLM.
            service.StatusStream.ObserveOnDispatcher().Subscribe(status =>
            {
                _status = status;
                if (status.State == AmdFlmState.Disabled || status.State == AmdFlmState.Starting)
                    _lastSample = null;
                RaisePropertyChanged(nameof(StatusMessage));
                RaisePropertyChanged(nameof(DiagnosticCounts));
                RaisePropertyChanged(nameof(LastMeasurement));
            });
            service.SampleStream.ObserveOnDispatcher().Subscribe(sample =>
            {
                _lastSample = sample;
                RaisePropertyChanged(nameof(LastMeasurement));
            });
        }

        private void Update(int? output = null, int? captureMode = null, double? x = null, double? y = null,
            double? width = null, double? height = null, double? threshold = null)
        {
            _settings = new AmdFlmSettings(output ?? _settings.CaptureOutputIndex, captureMode ?? _settings.CaptureMode,
                x ?? _settings.StartX, y ?? _settings.StartY, width ?? _settings.Width,
                height ?? _settings.Height, threshold ?? _settings.ThresholdCoefficient);
            if (_configuration.AmdFlmCaptureOutputIndex != _settings.CaptureOutputIndex)
                _configuration.AmdFlmCaptureOutputIndex = _settings.CaptureOutputIndex;
            if (_configuration.AmdFlmCaptureMode != _settings.CaptureMode)
                _configuration.AmdFlmCaptureMode = _settings.CaptureMode;
            if (_configuration.AmdFlmCaptureStartX != _settings.StartX)
                _configuration.AmdFlmCaptureStartX = _settings.StartX;
            if (_configuration.AmdFlmCaptureStartY != _settings.StartY)
                _configuration.AmdFlmCaptureStartY = _settings.StartY;
            if (_configuration.AmdFlmCaptureWidth != _settings.Width)
                _configuration.AmdFlmCaptureWidth = _settings.Width;
            if (_configuration.AmdFlmCaptureHeight != _settings.Height)
                _configuration.AmdFlmCaptureHeight = _settings.Height;
            if (_configuration.AmdFlmThresholdCoefficient != _settings.ThresholdCoefficient)
                _configuration.AmdFlmThresholdCoefficient = _settings.ThresholdCoefficient;
            RaisePropertyChanged(string.Empty);
        }
    }
}
