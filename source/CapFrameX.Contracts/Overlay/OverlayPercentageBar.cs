using System;
using System.Globalization;

namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// Shared percentage-bar semantics for the RTSS and CapFrameX renderers.
    /// Values stay unclamped here so text and limit colors retain the actual measurement.
    /// </summary>
    public static class OverlayPercentageBar
    {
        public static EOverlayValueDisplayMode GetDisplayMode(IOverlayEntry entry)
        {
            if (entry == null || entry.ValueUnitFormat?.Trim() != "%")
                return EOverlayValueDisplayMode.Text;

            return entry.ValueDisplayMode == EOverlayValueDisplayMode.Bar
                || entry.ValueDisplayMode == EOverlayValueDisplayMode.TextAndBar
                ? entry.ValueDisplayMode : EOverlayValueDisplayMode.Text;
        }

        public static bool TryGetValue(IOverlayEntry entry, out double value)
        {
            value = 0;
            var raw = entry?.Value;
            if (raw == null)
                return false;
            if (raw is string text)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    && !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                    return false;
            }
            else
            {
                switch (Type.GetTypeCode(raw.GetType()))
                {
                    case TypeCode.Byte:
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        if (raw.GetType().IsEnum)
                            return false;
                        value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                        break;
                    default:
                        return false;
                }
            }

            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static string GetColor(IOverlayEntry entry)
        {
            if (entry == null)
                return null;

            if (TryGetValue(entry, out var value))
            {
                if (double.TryParse(entry.UpperLimitValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var upper)
                    && value >= upper)
                    return entry.UpperLimitColor;
                if (double.TryParse(entry.LowerLimitValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var lower)
                    && value <= lower)
                    return entry.LowerLimitColor;
            }

            return entry.Color;
        }
    }
}
