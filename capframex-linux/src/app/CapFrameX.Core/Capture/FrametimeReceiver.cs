using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Shared.Models;

namespace CapFrameX.Core.Capture;

/// <summary>
/// Receives and buffers frametime data during capture
/// </summary>
public class FrametimeReceiver : IDisposable
{
    private readonly Subject<FrameData> _frameSubject = new();
    private readonly List<FrameData> _frameBuffer = new();
    private readonly object _bufferLock = new();

    private ulong _frameCount;
    private DateTime _captureStartTime;
    private bool _isCapturing;

    // Rolling statistics for live display
    private readonly Queue<float> _recentFrametimes = new();
    private const int RecentFrameCount = 300; // ~5 seconds at 60fps

    public IObservable<FrameData> Frames => _frameSubject.AsObservable();
    public bool IsCapturing => _isCapturing;
    public int BufferedFrameCount => _frameBuffer.Count;
    public TimeSpan CaptureDuration => _isCapturing ? DateTime.Now - _captureStartTime : TimeSpan.Zero;

    public void StartCapture()
    {
        lock (_bufferLock)
        {
            _frameBuffer.Clear();
            _recentFrametimes.Clear();
            _frameCount = 0;
            _captureStartTime = DateTime.Now;
            _isCapturing = true;
        }
    }

    public void StopCapture()
    {
        _isCapturing = false;
    }

    public void AddFrame(FrameDataPoint point)
    {
        var frame = new FrameData
        {
            FrameNumber = point.FrameNumber,
            TimestampNs = point.TimestampNs,
            FrametimeMs = point.FrametimeMs
        };

        lock (_bufferLock)
        {
            // Always update live stats rolling buffer
            _recentFrametimes.Enqueue(point.FrametimeMs);
            if (_recentFrametimes.Count > RecentFrameCount)
            {
                _recentFrametimes.Dequeue();
            }

            // Only buffer frames when capturing
            if (_isCapturing)
            {
                _frameBuffer.Add(frame);
                _frameCount++;
            }
        }

        _frameSubject.OnNext(frame);
    }

    public IReadOnlyList<FrameData> GetCapturedFrames()
    {
        lock (_bufferLock)
        {
            return _frameBuffer.ToList();
        }
    }

    public void ClearBuffer()
    {
        lock (_bufferLock)
        {
            _frameBuffer.Clear();
        }
    }

    /// <summary>
    /// Get live statistics from recent frames
    /// </summary>
    public LiveStats GetLiveStats()
    {
        lock (_bufferLock)
        {
            if (_recentFrametimes.Count == 0)
            {
                return new LiveStats();
            }

            var frametimes = _recentFrametimes.ToArray();
            var avgFrametime = frametimes.Average();
            var sortedFrametimes = frametimes.OrderByDescending(x => x).ToArray();

            // 1% low = average of worst 1% of frames
            var onePercentCount = Math.Max(1, frametimes.Length / 100);
            var onePercentLow = sortedFrametimes.Take(onePercentCount).Average();

            // 0.1% low
            var pointOnePercentCount = Math.Max(1, frametimes.Length / 1000);
            var pointOnePercentLow = sortedFrametimes.Take(pointOnePercentCount).Average();

            return new LiveStats
            {
                CurrentFps = avgFrametime > 0 ? 1000f / avgFrametime : 0,
                AverageFrametime = avgFrametime,
                P1Low = onePercentLow > 0 ? 1000f / onePercentLow : 0,
                P01Low = pointOnePercentLow > 0 ? 1000f / pointOnePercentLow : 0,
                FrameCount = _isCapturing ? _frameBuffer.Count : _recentFrametimes.Count,
                Duration = CaptureDuration
            };
        }
    }

    public void Dispose()
    {
        _frameSubject.Dispose();
    }
}

public record LiveStats
{
    public float CurrentFps { get; init; }
    public float AverageFrametime { get; init; }
    public float P1Low { get; init; }
    public float P01Low { get; init; }
    public int FrameCount { get; init; }
    public TimeSpan Duration { get; init; }
}
