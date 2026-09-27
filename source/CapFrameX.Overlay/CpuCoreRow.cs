using System.Globalization;
using System.Text.RegularExpressions;

namespace CapFrameX.Overlay
{
    /// <summary>
    /// A per-core CPU row as the sensor mapping names it: "Core #7 E (MHz)", "Core #7 E Thread #1 (%)",
    /// "Core #7 E (Effective) (MHz)" or, on homogeneous parts, "Core #7 (MHz)". <see cref="Label"/> is
    /// the core's identity including its hybrid type, <see cref="Family"/> is what the row shows for
    /// that core; every core of one CPU has the same families, and a template switches on whole
    /// families. Group names parse as well ("Core #7 E" has an empty family).
    /// </summary>
    internal sealed class CpuCoreRow
    {
        // The type is a single upper-case token (P, E, LPE, D, LP); the lookahead keeps "Thread"
        // from being read as a type on homogeneous parts.
        private static readonly Regex Pattern = new Regex(
            @"^Core #(?<index>\d+)(?: (?<type>[A-Z]+)(?=\s|$))?(?<family>.*)$", RegexOptions.Compiled);

        private CpuCoreRow(int index, string type, string family)
        {
            Index = index;
            Type = type;
            Family = family;
        }

        /// <summary>One-based core number as it appears in the row.</summary>
        public int Index { get; }

        /// <summary>Hybrid core type suffix, empty on homogeneous parts.</summary>
        public string Type { get; }

        /// <summary>The row's purpose without the core identity, e.g. "(MHz)" or "Thread #1 (%)".</summary>
        public string Family { get; }

        /// <summary>"Core #7 E", or "Core #7" without a type: the group name the sensor mapping derives.</summary>
        public string Label => Type.Length == 0 ? $"Core #{Index}" : $"Core #{Index} {Type}";

        public static bool TryParse(string text, out CpuCoreRow row)
        {
            row = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var match = Pattern.Match(text.Trim());
            if (!match.Success)
                return false;

            row = new CpuCoreRow(
                int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture),
                match.Groups["type"].Value,
                match.Groups["family"].Value.Trim());
            return true;
        }
    }
}
