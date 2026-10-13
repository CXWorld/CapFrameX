using System;
using CapFrameX.Contracts.Configuration;

namespace CapFrameX.OSD.Integration
{
    /// <summary>OLED pixel orbit parameters, from a design or from the global option.</summary>
    public readonly record struct OverlayPixelShift(int IntervalSeconds, int Radius);

    /// <summary>
    /// Adds the OLED pixel orbit to the configured margins. The active design's own request
    /// wins; the global option covers every other design and the classic rows. Both placement
    /// paths (hook / Vulkan layer channel and hook-free window) resolve through one instance and
    /// one monotonic clock, so they always agree on the current position.
    /// </summary>
    public sealed class OverlayPixelOrbitResolver
    {
        private readonly IAppConfiguration _configuration;
        private readonly Func<OverlayRuntimeDesign> _currentDesign;
        private readonly Func<long> _clock;

        public OverlayPixelOrbitResolver(IAppConfiguration configuration,
            Func<OverlayRuntimeDesign> currentDesign, Func<long> clock = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _currentDesign = currentDesign ?? (() => null);
            _clock = clock ?? (() => Environment.TickCount64);
        }

        public OverlayPixelShift? Current
        {
            get
            {
                var design = _currentDesign()?.PixelShift;
                if (design.HasValue) return design;
                return _configuration.OsdPixelOrbitEnabled
                    ? new OverlayPixelShift(_configuration.OsdPixelOrbitInterval, _configuration.OsdPixelOrbitRadius)
                    : null;
            }
        }

        public (int MarginX, int MarginY) Apply(int marginX, int marginY)
        {
            var shift = Current;
            if (!shift.HasValue) return (marginX, marginY);
            var (x, y) = OsdPixelOrbit.GetOffset(shift.Value.IntervalSeconds, shift.Value.Radius, _clock());
            return (marginX + x, marginY + y);
        }
    }
}
