using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Interop;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Maps CapFrameX <see cref="IOverlayEntry"/> items (as delivered by
    /// <c>IOverlayService.OSDUpdateNotifier</c> / <c>OnDictionaryUpdated</c>) to the
    /// renderer's neutral <see cref="OsdEntry"/> model. Only entries that are enabled
    /// and flagged for the overlay are forwarded.
    /// </summary>
    public static class OverlayEntryAdapter
    {
        public static List<OsdEntry> ToOsdEntries(IEnumerable<IOverlayEntry> entries)
        {
            var list = new List<OsdEntry>();
            if (entries == null) return list;

            foreach (var e in entries)
            {
                if (e == null || !e.ShowOnOverlay || !e.IsEntryEnabled) continue;

                var o = new OsdEntry
                {
                    Identifier = e.Identifier,
                    Group = e.GroupName ?? string.Empty,
                    Label = string.IsNullOrEmpty(e.Description) ? e.Identifier : e.Description,
                    Unit = ExtractUnit(e.ValueUnitFormat),
                    Color = OsdColor.FromCapFrameXHex(e.Color),
                    ShowGraph = e.ShowGraph,
                    Digits = ExtractDigits(e.ValueAlignmentAndDigits),
                    Separators = e.GroupSeparators,
                };

                // Data type comes from IOverlayEntry.IsNumeric ("value consists only of int or
                // double") — the same flag CapFrameX's own RTSS/websocket OSD (OSDController)
                // formats on, and it IS reliable here because we read CurrentOverlayEntries (the
                // processed, IsNumeric-populated list) rather than the raw OnDictionaryUpdated dict.
                // Refinement: also accept a genuinely boxed number when the flag is off, so numeric
                // sensors the flag doesn't cover (Fan/Mainboard aren't in SetHardwareIsNumericState)
                // still format; but a text value that only looks numeric (driver "551.86") stays text
                // because we do NOT string-parse unflagged values.
                double dv;
                bool numeric = e.IsNumeric
                    ? TryToDouble(e.Value, out dv)         // flagged: parse boxed number or numeric string
                    : TryUnboxNumber(e.Value, out dv);     // unflagged: only a real boxed number, never a string
                if (numeric)
                {
                    o.IsNumeric = true;
                    // No-data / unsupported online metrics (Average FPS, PC Latency, Animation
                    // Error) come through as double.NaN; a numeric metric with no value should
                    // read 0, not the C runtime's "-nan(ind)" text.
                    o.Value = (double.IsNaN(dv) || double.IsInfinity(dv)) ? 0.0 : dv;
                }
                else
                {
                    o.IsNumeric = false;
                    o.ValueText = e.Value?.ToString() ?? e.FormattedValue;
                }

                if (TryParseLimit(e.UpperLimitValue, out var up))
                {
                    o.HasUpper = true;
                    o.UpperLimit = up;
                    o.UpperColor = OsdColor.FromCapFrameXHex(e.UpperLimitColor, 0xE5534BFFu);
                }
                if (TryParseLimit(e.LowerLimitValue, out var lo))
                {
                    o.HasLower = true;
                    o.LowerLimit = lo;
                    o.LowerColor = OsdColor.FromCapFrameXHex(e.LowerLimitColor, 0xE5534BFFu);
                }

                list.Add(o);
            }
            return list;
        }

        private static bool TryToDouble(object value, out double result)
        {
            switch (value)
            {
                case double d: result = d; return true;
                case float f: result = f; return true;
                case int i: result = i; return true;
                case long l: result = l; return true;
                case null: result = 0; return false;
                default:
                    return double.TryParse(value.ToString(), NumberStyles.Any,
                                           CultureInfo.InvariantCulture, out result);
            }
        }

        // Only genuine boxed numeric types — deliberately NOT strings, so a text value that
        // merely parses as a number (e.g. a driver version "551.86") is kept as text.
        private static bool TryUnboxNumber(object value, out double result)
        {
            switch (value)
            {
                case double d: result = d; return true;
                case float f: result = f; return true;
                case int i: result = i; return true;
                case long l: result = l; return true;
                case short s: result = s; return true;
                case byte b: result = b; return true;
                case uint ui: result = ui; return true;
                case ulong ul: result = ul; return true;
                case decimal m: result = (double)m; return true;
                default: result = 0; return false;
            }
        }

        private static bool TryParseLimit(string s, out double v)
        {
            v = 0;
            return !string.IsNullOrWhiteSpace(s) &&
                   double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
        }

        // ValueAlignmentAndDigits is a string.Format pattern — either custom ("{0,5:0.0}",
        // count the zeros after the decimal point) or fixed-point ("{0,5:F1}", precision
        // follows the specifier) — recover the decimal digit count from both.
        private static int ExtractDigits(string fmt)
        {
            if (string.IsNullOrEmpty(fmt)) return 0;
            int dot = fmt.LastIndexOf('.');
            if (dot >= 0)
            {
                int n = 0;
                for (int i = dot + 1; i < fmt.Length && fmt[i] == '0'; i++) n++;
                return n;
            }
            int f = fmt.LastIndexOf('F');
            if (f < 0) f = fmt.LastIndexOf('f');
            if (f >= 0)
            {
                int n = 0, i = f + 1;
                for (; i < fmt.Length && fmt[i] >= '0' && fmt[i] <= '9'; i++)
                    n = n * 10 + (fmt[i] - '0');
                if (i > f + 1) return n;
            }
            return 0;
        }

        // ValueUnitFormat may carry the unit with RTSS markup tags (<...>); strip them.
        private static string ExtractUnit(string unitFormat)
        {
            if (string.IsNullOrEmpty(unitFormat)) return string.Empty;
            var sb = new StringBuilder();
            bool inTag = false;
            foreach (var ch in unitFormat)
            {
                if (ch == '<') inTag = true;
                else if (ch == '>') inTag = false;
                else if (!inTag && ch != '{' && ch != '}' && ch != '0') sb.Append(ch);
            }
            return sb.ToString().Trim();
        }
    }
}
