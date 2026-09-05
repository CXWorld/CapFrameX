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
        public static List<OsdEntry> ToOsdEntries(IEnumerable<IOverlayEntry> entries,
            bool showRunHistory = false,
            IReadOnlyList<string> runHistory = null,
            IReadOnlyList<bool> runHistoryOutlierFlags = null,
            string runHistoryAggregation = null)
        {
            var list = new List<OsdEntry>();
            if (entries == null) return list;

            foreach (var e in entries)
            {
                if (e == null || !e.ShowOnOverlay || !e.IsEntryEnabled) continue;

                // RTSS expands this placeholder from its own run-history state. The CX renderers
                // receive that state explicitly and create one real row per configured run, so an
                // inactive history never leaks an otherwise empty "Run history" group.
                if (e.Identifier == "RunHistory")
                {
                    if (showRunHistory)
                        AddRunHistoryEntries(list, e, runHistory, runHistoryOutlierFlags,
                            runHistoryAggregation);
                    continue;
                }

                var o = new OsdEntry
                {
                    Identifier = e.Identifier,
                    Group = e.GroupName ?? string.Empty,
                    Label = string.IsNullOrEmpty(e.Description) ? e.Identifier : e.Description,
                    Unit = ExtractUnit(e.ValueUnitFormat),
                    Color = OsdColor.FromCapFrameXHex(e.Color),
                    GroupColor = OsdColor.FromCapFrameXHex(e.GroupColor),
                    ShowGraph = e.ShowGraph,
                    Digits = ExtractDigits(e.ValueAlignmentAndDigits),
                    Separators = e.GroupSeparators,
                };

                // Data type comes from IOverlayEntry.IsNumeric ("value consists only of int or
                // double") — the same flag CapFrameX's own RTSS/websocket OSD (OSDController)
                // formats on, and it is reliable here because the display list has already been
                // processed and its IsNumeric flags populated before publication.
                // Refinement: also accept a genuinely boxed number when the flag is off, so numeric
                // sensors the flag doesn't cover (Fan/Mainboard aren't in SetHardwareIsNumericState)
                // still format; but a text value that only looks numeric (driver "551.86") stays text
                // because we do NOT string-parse unflagged values.
                double dv = 0;
                bool numeric;
                if (e.IsNumeric)
                {
                    // IsNumeric describes the entry's data type, not whether this particular
                    // snapshot already has a value. Transient values are not persisted, so a
                    // freshly loaded numeric entry (notably DisplayTime) can legitimately be null.
                    // Keep it numeric and publish 0 until its live source supplies a sample.
                    numeric = true;
                    TryToDouble(e.Value, out dv);
                }
                else
                {
                    // Unflagged: only a real boxed number, never a numeric-looking string.
                    numeric = TryUnboxNumber(e.Value, out dv);
                }
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
                    // FormattedValue contains RTSS hypertext (<S...>/<C...>). The neutral OSD
                    // renderer does not interpret those tags, so only forward the raw text value.
                    o.ValueText = e.Value?.ToString() ?? string.Empty;
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

        private static void AddRunHistoryEntries(List<OsdEntry> list, IOverlayEntry template,
            IReadOnlyList<string> runHistory, IReadOnlyList<bool> outlierFlags,
            string aggregation)
        {
            if (runHistory == null) return;

            uint valueColor = OsdColor.FromCapFrameXHex(template.Color);
            uint groupColor = OsdColor.FromCapFrameXHex(template.GroupColor);
            uint outlierColor = OsdColor.FromCapFrameXHex(template.LowerLimitColor, 0xC80000FFu);

            for (int i = 0; i < runHistory.Count; i++)
            {
                bool isOutlier = outlierFlags != null && i < outlierFlags.Count && outlierFlags[i];
                list.Add(new OsdEntry
                {
                    Identifier = $"RunHistory.{i + 1}",
                    Group = $"Run {i + 1}:",
                    Label = template.Description ?? "Run history",
                    ValueText = string.IsNullOrEmpty(runHistory[i]) ? "N/A" : runHistory[i],
                    IsNumeric = false,
                    Color = isOutlier ? outlierColor : valueColor,
                    GroupColor = groupColor,
                    Separators = i == 0 ? template.GroupSeparators : 0
                });
            }

            if (!string.IsNullOrWhiteSpace(aggregation))
            {
                list.Add(new OsdEntry
                {
                    Identifier = "RunHistory.Result",
                    Group = "Result:",
                    Label = template.Description ?? "Run history",
                    ValueText = aggregation,
                    IsNumeric = false,
                    Color = valueColor,
                    GroupColor = groupColor
                });
            }
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
            foreach (var ch in unitFormat.Replace("{0}", string.Empty))
            {
                if (ch == '<') inTag = true;
                else if (ch == '>') inTag = false;
                else if (!inTag && ch != '{' && ch != '}') sb.Append(ch);
            }
            return sb.ToString().Trim();
        }
    }
}
