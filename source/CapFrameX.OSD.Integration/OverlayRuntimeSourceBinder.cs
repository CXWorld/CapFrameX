using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CapFrameX.Contracts.Localization;
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
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_LayoutDimensions"), message => new FormatException(message));
            bool changed = false;
            BindTiles(root, tiles);
            return changed ? OverlayRuntimeDesign.Parse(design.ProfileId, design.Name, document.ToString(Formatting.None)) : design;

            void BindTiles(JObject canvas, JArray frames)
            {
                foreach (var outer in frames.OfType<JObject>())
                {
                    if (outer["children"] is not JArray children || children.FirstOrDefault() is not JObject widget
                        || widget["editor"] is not JObject editor) continue;
                    string kind = (string)editor["kind"];
                    string role = (string)editor["groupKey"];
                    if (kind == "container" && widget["children"] is JArray nested)
                    {
                        double height = (double?)widget["height"] ?? 0;
                        BindTiles(widget, nested);
                        if ((double?)widget["height"] > height)
                            GrowFrame(canvas, frames, outer, (double)widget["height"] + 2 * ((double?)outer["padding"] ?? 0), scale, gridSize);
                    }
                    else if (kind == "group" && role?.StartsWith("role:", StringComparison.Ordinal) == true)
                    {
                        string family = role.Substring(5);
                        var matches = groups.Where(group => group.All(source => source.GroupKey == family
                            || source.GroupKey.EndsWith("/" + family, StringComparison.Ordinal))).Take(2).ToArray();
                        if (matches.Length != 1) continue;
                        ExpandGroup(canvas, frames, outer, widget, editor, matches[0], scale, gridSize);
                        changed = true;
                    }
                    else if (kind != "group" && kind != "text" && kind != "chart" && kind != "classicRows" && kind != "icon" && kind != "image")
                    {
                        BindKey(widget, "key", "unit", "valueScale", "valueFormat");
                        BindKey(widget, "secondaryKey", "secondaryUnit", "secondaryScale", null);
                    }
                }
            }

            void BindKey(JObject widget, string keyField, string unitField, string scaleField, string formatField)
            {
                string key = (string)widget[keyField];
                if (string.IsNullOrEmpty(key)) return;
                var matches = available.Where(source => !string.IsNullOrEmpty(source.SemanticKey) && source.SemanticKey == key)
                    .GroupBy(source => source.Id, StringComparer.Ordinal).Select(group => group.First()).Take(2).ToArray();
                if (matches.Length != 1) return;
                widget[keyField] = matches[0].Id;
                if (matches[0].Unit != null && widget[scaleField] == null && (formatField == null || widget[formatField] == null))
                    widget[unitField] = matches[0].Unit;
                changed = true;
            }
        }

        private static void ExpandGroup(JObject root, JArray tiles, JObject outer, JObject widget, JObject editor,
            OverlayTelemetrySource[] sources, double scale, double gridSize)
        {
            if ((string)editor["groupDisplay"] == "heatmap")
            {
                ExpandHeatmap(root, tiles, outer, widget, editor, sources, scale, gridSize);
                return;
            }
            if (widget["children"] is not JArray sections || sections.Count != 2 || sections[1] is not JObject grid
                || grid["children"] is not JArray oldMembers || oldMembers.FirstOrDefault() is not JObject prototype)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_GroupLayout"), message => new FormatException(message));
            string display = (string)editor["groupDisplay"];
            bool vertical = display == "bar" && (string)editor["groupBarOrientation"] == "vertical";
            bool showValues = (bool?)editor["groupShowValues"] ?? true;
            bool showLabels = (bool?)editor["groupShowLabels"] ?? true;
            bool hybrid = sources.Select(source => CoreType(source.CoreType)).Where(type => type != null)
                .Distinct(StringComparer.Ordinal).Take(2).Count() > 1;
            bool initialBars = display == "bar" && editor["groupBarWidth"] == null && editor["groupBarHeight"] == null
                && (bool?)editor["groupByCoreType"] != true;
            if (initialBars && editor["groupByCoreType"] == null && hybrid) editor["groupByCoreType"] = true;
            bool groupByCoreType = display == "bar" && (bool?)editor["groupByCoreType"] == true;
            if (groupByCoreType)
            {
                sources = sources.OrderBy(source => CoreTypeOrder(source.CoreType))
                    .ThenBy(source => source.CoreIndex ?? int.MaxValue)
                    .ThenBy(source => source.ThreadIndex ?? int.MaxValue).ToArray();
            }
            string unit = sources[0].Unit ?? "";
            var labels = sources.Select(source => showLabels
                ? (CoreType(source.CoreType) is string type ? (type == "LPE" ? "LP-E" : type) + " " : "") + CompactLabel(source.Name)
                : "").ToArray();
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
                minimumWidth = !showLabels && !showValues ? 8 : Math.Max(24, Math.Max(labelWidth, valueWidth));
            }
            else if (display == "bar") minimumWidth = Math.Max(115, (labelLength + numberLength + unit.Length + 2) * 8 + 12);
            else minimumWidth = Math.Max(115, Math.Max(labelLength * 8 + 8, 6 * valueSize + unit.Length * 8 + 8));
            double padding = (double?)outer["padding"] ?? 0;
            int rootColumns = (int?)root["columns"] ?? 1;
            int columnSpan = (int?)outer["colSpan"] ?? 1;
            double rootGap = (double?)root["gap"] ?? 0;
            double flowWidth = (((double?)root["width"] ?? 0) - rootGap * (rootColumns - 1)) / rootColumns;
            double outerWidth = (double?)outer["frameWidth"] ?? flowWidth * columnSpan + rootGap * (columnSpan - 1);
            double innerWidth = outerWidth - 2 * padding;
            if (!double.IsFinite(innerWidth) || innerWidth <= 0)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_GroupWidth"), message => new FormatException(message));
            double gap = (vertical ? 4 : 8) * scale;
            int fitting = Math.Max(1, (int)Math.Floor((innerWidth + gap) / (minimumWidth * scale + gap)));
            int columns = Math.Min(Math.Min((int?)editor["groupColumns"] ?? 2, sources.Length), fitting);
            double width = vertical ? innerWidth : Math.Max(innerWidth, minimumWidth * scale);
            double cellWidth = (width - gap * (columns - 1)) / columns;
            if (initialBars)
            {
                // Portable templates choose a balanced initial grid after discovering the
                // actual sensor count. Explicit user dimensions keep their normal wrapping.
                double availableWidth = innerWidth / scale;
                double memberGap = vertical ? 4 : 8;
                int preferredColumns = groupByCoreType ? HybridTemplateColumns(sources) : vertical
                    ? sources.Length <= 2 ? sources.Length : Math.Min(32, (sources.Length + 1) / 2)
                    : columns;
                int fittingColumns = vertical
                    ? Math.Max(1, (int)Math.Floor((availableWidth + memberGap) / (8 + memberGap)))
                    : columns;
                int limit = Math.Min(32, Math.Min(sources.Length, Math.Min(preferredColumns, fittingColumns)));
                int balancedColumns = groupByCoreType ? limit : BalancedTemplateColumns(sources.Length, limit);
                editor["groupColumns"] = balancedColumns;
                editor["groupBarWidth"] = (availableWidth - memberGap * (balancedColumns - 1)) / balancedColumns;
                editor["groupBarHeight"] = vertical ? barHeight : 8;
            }
            if (display == "bar" && (groupByCoreType || (double?)editor["groupBarWidth"] != null || (double?)editor["groupBarHeight"] != null))
            {
                ExpandSizedBars(root, tiles, outer, grid, prototype, editor, sources, labels, unit,
                    vertical, showValues, innerWidth, cellWidth, scale, gridSize);
                return;
            }
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
            GrowFrame(root, tiles, outer, required, scale, gridSize);
        }

        private static int BalancedTemplateColumns(int count, int preferredColumns)
        {
            int limit = Math.Max(1, Math.Min(count, preferredColumns));
            for (int columns = limit; columns >= 2; columns--)
            {
                if (columns % 2 == 0 && count % columns == 0 &&
                    (count / columns == 1 || count / columns % 2 == 0)) return columns;
            }
            for (int columns = limit; columns >= 1; columns--)
            {
                if (count % columns == 0 && (count / columns == 1 || count / columns % 2 == 0)) return columns;
            }
            return limit;
        }

        private static string CoreType(string value) => value?.Trim().ToUpperInvariant() switch
        {
            "P" => "P", "E" => "E", "LPE" => "LPE", "D" => "D", "LP" => "LP", _ => null
        };

        private static int CoreTypeOrder(string value) => CoreType(value) switch
        {
            "P" => 0, "E" => 1, "LPE" => 2, "D" => 3, "LP" => 4, _ => 5
        };

        private static int HybridTemplateColumns(OverlayTelemetrySource[] sources)
        {
            var sections = sources.Where(source => CoreType(source.CoreType) != null).GroupBy(source => CoreType(source.CoreType))
                .OrderBy(section => CoreTypeOrder(section.Key)).ToArray();
            int PhysicalCount(IEnumerable<OverlayTelemetrySource> readings) => readings.All(source => source.CoreIndex.HasValue)
                ? readings.Select(source => source.CoreIndex.Value).Distinct().Count() : readings.Count();
            int primary = PhysicalCount(sections.First());
            int largest = sections.Max(section => PhysicalCount(section));
            int preferred = Math.Max(primary, (largest + 1) / 2);
            int columns = 1;
            while (columns < preferred && columns < 32) columns *= 2;
            return columns;
        }

        private static void ExpandSizedBars(JObject root, JArray tiles, JObject outer, JObject canvas, JObject prototype,
            JObject editor, OverlayTelemetrySource[] sources, string[] labels, string unit, bool vertical,
            bool showValues, double innerWidth, double legacyCellWidth, double scale, double gridSize)
        {
            double requestedWidth = (double?)editor["groupBarWidth"] ?? legacyCellWidth / scale;
            double trackHeight = (double?)editor["groupBarHeight"] ?? (vertical ? (double?)editor["height"] ?? 32 : 8);
            if (!double.IsFinite(requestedWidth) || requestedWidth < 1 || requestedWidth > 1600
                || !double.IsFinite(trackHeight) || trackHeight < 1 || trackHeight * scale > 4096)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_GroupBars"), message => new FormatException(message));
            double availableWidth = innerWidth / scale;
            double gap = vertical ? 4 : 8;
            int columns = Math.Min(sources.Length, Math.Max(1,
                (int)Math.Floor((availableWidth + gap) / (requestedWidth + gap) + 1e-9)));
            bool groupByCoreType = (bool?)editor["groupByCoreType"] == true;
            var sections = groupByCoreType
                ? sources.GroupBy(source => CoreType(source.CoreType)).Select(section => section.Count()).ToArray()
                : new[] { sources.Length };
            int rows = sections.Sum(count => (int)Math.Ceiling((double)count / columns));
            double cellWidth = Math.Min(requestedWidth, availableWidth);
            bool hasLabels = labels.Any(label => !string.IsNullOrEmpty(label));
            double cellHeight = trackHeight + (vertical
                ? (hasLabels ? 19.6 : 0) + (showValues ? 19.6 : 0)
                : (hasLabels || showValues ? 22.2 : 0));
            double canvasHeight = rows * cellHeight + (rows - 1) * gap;
            var members = new JArray();
            int row = 0, column = 0;
            for (int index = 0; index < sources.Length; index++)
            {
                if (index > 0 && (column == columns || groupByCoreType &&
                    CoreType(sources[index].CoreType) != CoreType(sources[index - 1].CoreType)))
                {
                    row++;
                    column = 0;
                }
                var member = (JObject)prototype.DeepClone();
                member["key"] = sources[index].Id;
                member["label"] = labels[index];
                member["unit"] = unit;
                member["width"] = cellWidth * scale;
                member["height"] = trackHeight * scale;
                member["x"] = column * (cellWidth + gap) * scale;
                member["y"] = row * (cellHeight + gap) * scale;
                member["frameWidth"] = cellWidth * scale;
                member["frameHeight"] = cellHeight * scale;
                members.Add(member);
                column++;
            }
            canvas["layout"] = "canvas";
            canvas.Remove("columns");
            canvas["children"] = members;
            canvas["width"] = innerWidth;
            canvas["height"] = canvasHeight * scale;
            if ((string)root["layout"] != "canvas") return;

            double padding = (double?)outer["padding"] ?? 0;
            double contentHeight = 18.2 + gap + canvasHeight;
            double required = Math.Max(24, Math.Ceiling((contentHeight + 2 * padding / scale) / gridSize) * gridSize) * scale;
            GrowFrame(root, tiles, outer, required, scale, gridSize);
        }

        // Matches the editor's heatmap group: members ordered P-cores first with each core's threads
        // together, one averaged key per cell, core types as labelled blocks, and a frame grown to the
        // flow height when too small.
        private static void ExpandHeatmap(JObject root, JArray tiles, JObject outer, JObject widget, JObject editor,
            OverlayTelemetrySource[] sources, double scale, double gridSize)
        {
            if (widget["children"] is not JArray children || children.Count != 1 || children[0] is not JObject heatmap
                || (string)heatmap["type"] != "heatmap")
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_GroupLayout"), message => new FormatException(message));
            var ordered = sources.Select((source, index) => (Source: source, Index: index))
                .OrderBy(item => CoreTypeOrder(item.Source.CoreType))
                .ThenBy(item => item.Source.CoreIndex ?? int.MaxValue)
                .ThenBy(item => item.Source.ThreadIndex ?? int.MaxValue)
                .ThenBy(item => item.Index).Select(item => item.Source).ToArray();
            int seconds = (int?)editor["groupWindowSeconds"] ?? DefaultHeatmapWindowSeconds;
            heatmap["keys"] = new JArray(ordered.Select(source => OverlayMovingAverages.Key(seconds, source.Id)));
            string[] types = ordered.Select(source => CoreType(source.CoreType) ?? "-").ToArray();
            if (types.Any(type => type != "-")) heatmap["types"] = new JArray(types);
            else heatmap.Remove("types");

            int rows = HeatmapRows(types);
            double labels = types.Any(type => type != "-") ? 13 * .85 * 1.3 + 2 : 0;
            double flowHeight = 13 * 1.3 + 4 + labels + rows * 14 + (rows - 1) * 2;
            if ((string)root["layout"] != "canvas")
            {
                heatmap["height"] = flowHeight * scale;
                return;
            }
            double padding = (double?)outer["padding"] ?? 0;
            double required = Math.Max(24, Math.Ceiling((flowHeight + 2 * padding / scale) / gridSize) * gridSize) * scale;
            if (required <= ((double?)outer["frameHeight"] ?? 0)) return;
            GrowFrame(root, tiles, outer, required, scale, gridSize);
            heatmap["height"] = required - 2 * padding;
        }

        private const int DefaultHeatmapWindowSeconds = 60;
        private const int HeatmapColumns = 16;

        // The renderer's ArrangeHeatmap: every run of one core type is a block, up to 16 cells share
        // one row, and more take the fewest rows at which the blocks need at most 16 columns.
        internal static int HeatmapRows(IReadOnlyList<string> types)
        {
            if (types.Count <= HeatmapColumns) return 1;
            var blocks = new List<int>();
            for (int start = 0; start < types.Count;)
            {
                int end = start + 1;
                while (end < types.Count && types[end] == types[start]) end++;
                blocks.Add(end - start);
                start = end;
            }
            int longest = blocks.Max();
            for (int rows = 2; rows < longest; rows++)
                if (blocks.Sum(count => (count + rows - 1) / rows) <= HeatmapColumns) return rows;
            return longest;
        }

        private static void GrowFrame(JObject root, JArray tiles, JObject outer, double required, double scale, double gridSize)
        {
            double originalHeight = (double?)outer["frameHeight"] ?? 0;
            if (required <= originalHeight) return;
            double x = (double?)outer["x"] ?? 0, y = (double?)outer["y"] ?? 0;
            double right = x + (double)outer["frameWidth"];
            double increase = required - originalHeight;
            var following = tiles.OfType<JObject>().Where(tile => !ReferenceEquals(tile, outer)
                && (double?)tile["y"] >= y + originalHeight - .001 * scale
                && (double?)tile["x"] < right && (double?)tile["x"] + (double?)tile["frameWidth"] > x).ToArray();
            double bottom = Math.Max(y + required, following.Select(tile => (double)tile["y"] + (double)tile["frameHeight"] + increase).DefaultIfEmpty().Max());
            double needed = Math.Ceiling((bottom / scale + gridSize) / gridSize) * gridSize * scale;
            if (needed > 4096 * scale) throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_GroupHeight"), message => new FormatException(message));
            outer["frameHeight"] = required;
            foreach (var next in following) next["y"] = (double)next["y"] + increase;
            root["height"] = Math.Max((double?)root["height"] ?? 0, needed);
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
