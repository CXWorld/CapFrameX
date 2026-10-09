using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Binds portable preset roles without loading the optional WPF editor. The saved document
    /// remains unchanged; editor/runtime parity tests cover group columns and canvas expansion.
    /// </summary>
    internal static class OverlayRuntimeSourceBinder
    {
        internal static OverlayRuntimeDesign Bind(OverlayRuntimeDesign design, IReadOnlyList<OverlayTelemetrySource> catalog)
        {
            if (design == null || catalog == null || catalog.Count == 0) return design;
            var document = JObject.Parse(design.AuthoringJson);
            if ((string)document["editor"]?["format"] != "cfx-grid" || document["root"] is not JObject root
                || root["children"] is not JArray tiles) return design;
            var available = catalog.Where(source => source != null && source.IsAvailable && !string.IsNullOrWhiteSpace(source.Id)).ToArray();
            var groups = available.Where(source => !source.IsText && !string.IsNullOrWhiteSpace(source.GroupKey))
                .GroupBy(source => new { Device = source.DeviceKey ?? source.HardwareName ?? "", source.GroupKey, Unit = source.Unit ?? "", source.Category })
                .Select(group => group.GroupBy(source => source.Id, StringComparer.Ordinal).Select(values => values.First())
                    .OrderBy(source => source.GroupOrder).ThenBy(source => source.Name, NaturalComparer.Instance)
                    .ThenBy(source => source.Id, StringComparer.Ordinal).ToArray())
                .Where(group => group.Length >= 2 && group.Length <= 128).ToArray();
            double scale = (double?)document["editor"]?["scale"] ?? 1;
            double gridSize = (double?)document["editor"]?["gridSize"] ?? 8;
            if (!double.IsFinite(scale) || scale < .5 || scale > 2 || !double.IsFinite(gridSize) || gridSize < 1 || gridSize > 128)
                throw new FormatException("The preset source layout has invalid scale or grid dimensions.");
            bool changed = false;
            foreach (var outer in tiles.OfType<JObject>())
            {
                if (outer["children"] is not JArray children || children.FirstOrDefault() is not JObject widget
                    || widget["editor"] is not JObject editor) continue;
                string kind = (string)editor["kind"];
                string role = (string)editor["groupKey"];
                if (kind == "group" && role?.StartsWith("role:", StringComparison.Ordinal) == true)
                {
                    string family = role.Substring(5);
                    var matches = groups.Where(group => group.All(source => source.GroupKey == family
                        || source.GroupKey.EndsWith("/" + family, StringComparison.Ordinal))).Take(2).ToArray();
                    if (matches.Length != 1) continue;
                    ExpandGroup(root, tiles, outer, widget, editor, matches[0], scale, gridSize);
                    changed = true;
                }
                else if (kind != "group" && kind != "text" && kind != "chart" && kind != "classicRows")
                {
                    string key = (string)widget["key"];
                    var matches = available.Where(source => !string.IsNullOrEmpty(source.SemanticKey) && source.SemanticKey == key)
                        .GroupBy(source => source.Id, StringComparer.Ordinal).Select(group => group.First()).Take(2).ToArray();
                    if (matches.Length != 1) continue;
                    widget["key"] = matches[0].Id;
                    if (matches[0].Unit != null) widget["unit"] = matches[0].Unit;
                    changed = true;
                }
            }
            return changed ? OverlayRuntimeDesign.Parse(design.ProfileId, design.Name, document.ToString(Formatting.None)) : design;
        }

        private static void ExpandGroup(JObject root, JArray tiles, JObject outer, JObject widget, JObject editor,
            OverlayTelemetrySource[] sources, double scale, double gridSize)
        {
            if (widget["children"] is not JArray sections || sections.Count != 2 || sections[1] is not JObject grid
                || grid["children"] is not JArray oldMembers || oldMembers.FirstOrDefault() is not JObject prototype)
                throw new FormatException("The preset sensor group has no native member layout.");
            string display = (string)editor["groupDisplay"];
            bool vertical = display == "bar" && (string)editor["groupBarOrientation"] == "vertical";
            bool showValues = (bool?)editor["groupShowValues"] ?? true;
            string unit = sources[0].Unit ?? "";
            var labels = sources.Select(source => CompactLabel(source.Name)).ToArray();
            int digits = (int?)editor["digits"] ?? 0;
            double valueSize = (double?)editor["valueSize"] ?? 14;
            double barHeight = (double?)editor["height"] ?? 32;
            string format = "F" + digits;
            int numberLength = Math.Max(((double?)editor["minimum"] ?? 0).ToString(format, CultureInfo.InvariantCulture).Length,
                ((double?)editor["maximum"] ?? 100).ToString(format, CultureInfo.InvariantCulture).Length);
            int labelLength = labels.Max(label => label.Length);
            double minimumWidth;
            if (vertical)
            {
                double labelWidth = Math.Min(72, labelLength * 6.6 + 6);
                double valueWidth = showValues ? (numberLength + unit.Length + 1) * 6.6 + 6 : 0;
                minimumWidth = Math.Max(24, Math.Max(labelWidth, valueWidth));
            }
            else if (display == "bar") minimumWidth = Math.Max(115, (labelLength + numberLength + unit.Length + 2) * 8 + 12);
            else minimumWidth = Math.Max(115, Math.Max(labelLength * 8 + 8, 6 * valueSize + unit.Length * 8 + 8));
            double padding = (double?)outer["padding"] ?? 0;
            int rootColumns = (int?)root["columns"] ?? 1;
            int columnSpan = (int?)outer["colSpan"] ?? 1;
            double rootGap = (double?)root["gap"] ?? 0;
            double flowWidth = (((double?)root["width"] ?? 0) - rootGap * (rootColumns - 1)) / rootColumns;
            double outerWidth = (double?)outer["frameWidth"] ?? flowWidth * columnSpan + rootGap * (columnSpan - 1);
            double innerWidth = Math.Max(64 * scale, outerWidth - 2 * padding);
            double gap = (vertical ? 4 : 8) * scale;
            int fitting = Math.Max(1, (int)Math.Floor((innerWidth + gap) / (minimumWidth * scale + gap)));
            int columns = Math.Min(Math.Min((int?)editor["groupColumns"] ?? 2, sources.Length), fitting);
            double width = vertical ? innerWidth : Math.Max(innerWidth, minimumWidth * scale);
            double cellWidth = (width - gap * (columns - 1)) / columns;
            var members = new JArray();
            for (int i = 0; i < sources.Length; i++)
            {
                var member = (JObject)prototype.DeepClone();
                member["key"] = sources[i].Id;
                member["label"] = labels[i];
                member["unit"] = unit;
                member["width"] = cellWidth;
                members.Add(member);
            }
            grid["children"] = members;
            grid["columns"] = columns;
            grid["width"] = width;
            if ((string)root["layout"] != "canvas") return;

            int rows = (int)Math.Ceiling((double)sources.Length / columns);
            double contentHeight;
            if (vertical)
            {
                double textSlot = 12 * 1.3 + 4;
                double cellHeight = barHeight + (labels.Any(label => label.Length > 0) ? textSlot : 0) + (showValues ? textSlot : 0);
                contentHeight = 13 * 1.4 + 4 + rows * cellHeight + (rows - 1) * 4;
            }
            else contentHeight = 28 + rows * (display == "bar" ? 40 : valueSize * 1.4 + 28);
            double required = Math.Max(24, Math.Ceiling((contentHeight + 2 * padding / scale) / gridSize) * gridSize) * scale;
            double originalHeight = (double?)outer["frameHeight"] ?? 0;
            if (required <= originalHeight) return;
            double x = (double?)outer["x"] ?? 0, y = (double?)outer["y"] ?? 0;
            double right = x + (double)outer["frameWidth"];
            double increase = required - originalHeight;
            var following = tiles.OfType<JObject>().Where(tile => !ReferenceEquals(tile, outer)
                && (double?)tile["y"] >= y + originalHeight - .001 * scale
                && (double?)tile["x"] < right && (double?)tile["x"] + (double?)tile["frameWidth"] > x).ToArray();
            double bottom = Math.Max(y + required, following.Select(tile => (double)tile["y"] + (double)tile["frameHeight"] + increase).DefaultIfEmpty().Max());
            if (bottom > 4096 * scale) throw new FormatException("The sensor group needs more height. Widen its tile or increase its columns.");
            outer["frameHeight"] = required;
            foreach (var next in following) next["y"] = (double)next["y"] + increase;
            root["height"] = Math.Min(4096 * scale, Math.Max((double?)root["height"] ?? 0,
                Math.Ceiling((bottom / scale + gridSize) / gridSize) * gridSize * scale));
        }

        private static string CompactLabel(string name)
        {
            var core = Regex.Match(name ?? "", @"Core\s*#?\s*(?<core>\d+)", RegexOptions.IgnoreCase);
            var thread = Regex.Match(name ?? "", @"Thread\s*#?\s*(?<thread>\d+)", RegexOptions.IgnoreCase);
            if (core.Success) return "C" + core.Groups["core"].Value + (thread.Success ? " T" + thread.Groups["thread"].Value : "");
            if (thread.Success) return "T" + thread.Groups["thread"].Value;
            string label = Regex.Replace(name ?? "Reading", @"^CPU\s+", "", RegexOptions.IgnoreCase);
            label = Regex.Replace(label, @"Core\s*#?", "C", RegexOptions.IgnoreCase);
            return Regex.Replace(label, @"Thread\s*#?", "T", RegexOptions.IgnoreCase);
        }

        private sealed class NaturalComparer : IComparer<string>
        {
            public static readonly NaturalComparer Instance = new NaturalComparer();
            public int Compare(string x, string y)
            {
                x ??= ""; y ??= "";
                int a = 0, b = 0;
                while (a < x.Length && b < y.Length)
                {
                    if (char.IsDigit(x[a]) && char.IsDigit(y[b]))
                    {
                        int startA = a, startB = b;
                        while (a < x.Length && char.IsDigit(x[a])) a++;
                        while (b < y.Length && char.IsDigit(y[b])) b++;
                        string digitsA = x.Substring(startA, a - startA).TrimStart('0');
                        string digitsB = y.Substring(startB, b - startB).TrimStart('0');
                        int compared = digitsA.Length.CompareTo(digitsB.Length);
                        if (compared == 0) compared = string.CompareOrdinal(digitsA, digitsB);
                        if (compared != 0) return compared;
                    }
                    else
                    {
                        int compared = char.ToUpperInvariant(x[a++]).CompareTo(char.ToUpperInvariant(y[b++]));
                        if (compared != 0) return compared;
                    }
                }
                return (x.Length - a).CompareTo(y.Length - b);
            }
        }
    }
}
