using System;
using CapFrameX.Contracts.Configuration;

namespace CapFrameX.Contracts.Latency
{
    public sealed class AmdFlmSettings
    {
        public int CaptureOutputIndex { get; }
        public int CaptureMode { get; }
        public double StartX { get; }
        public double StartY { get; }
        public double Width { get; }
        public double Height { get; }
        public double ThresholdCoefficient { get; }

        public AmdFlmSettings(int outputIndex, int captureMode, double startX, double startY,
            double width, double height, double thresholdCoefficient)
        {
            CaptureOutputIndex = Math.Clamp(outputIndex, 0, 31);
            CaptureMode = Math.Clamp(captureMode, 0, 2);
            StartX = Normalize(startX, 0.40, 0, 0.99);
            StartY = Normalize(startY, 0.45, 0, 0.99);
            Width = Normalize(width, 0.20, 0.01, 1 - StartX);
            Height = Normalize(height, 0.25, 0.01, 1 - StartY);
            ThresholdCoefficient = Normalize(thresholdCoefficient, 3, 1, 10);
        }

        public static AmdFlmSettings FromConfiguration(IAppConfiguration config)
        {
            return new AmdFlmSettings(config.AmdFlmCaptureOutputIndex, config.AmdFlmCaptureMode,
                config.AmdFlmCaptureStartX, config.AmdFlmCaptureStartY,
                config.AmdFlmCaptureWidth, config.AmdFlmCaptureHeight, config.AmdFlmThresholdCoefficient);
        }

        public static bool IsConfigurationKey(string key)
        {
            return key == nameof(IAppConfiguration.UseAmdFlmLatency)
                || key == nameof(IAppConfiguration.AmdFlmCaptureOutputIndex)
                || key == nameof(IAppConfiguration.AmdFlmCaptureMode)
                || key == nameof(IAppConfiguration.AmdFlmCaptureStartX)
                || key == nameof(IAppConfiguration.AmdFlmCaptureStartY)
                || key == nameof(IAppConfiguration.AmdFlmCaptureWidth)
                || key == nameof(IAppConfiguration.AmdFlmCaptureHeight)
                || key == nameof(IAppConfiguration.AmdFlmThresholdCoefficient);
        }

        private static double Normalize(double value, double fallback, double minimum, double maximum)
        {
            return Math.Clamp(double.IsFinite(value) ? value : fallback, minimum, Math.Max(minimum, maximum));
        }
    }
}
