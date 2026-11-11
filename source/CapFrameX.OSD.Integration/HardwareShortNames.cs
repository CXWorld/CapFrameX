using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// CPU and GPU names for narrow overlay fields, like RTSS's %CPUShort% and %GPU%: trademarks,
    /// vendor words and clock, core-count or graphics suffixes go, the model stays
    /// ("Intel(R) Arc(TM) 140V GPU (16GB)" is "Arc 140V", "AMD Ryzen 7 9800X3D 8-Core Processor" is
    /// "R7 9800X3D"). Only these known parts are removed, so a custom label passes unchanged.
    /// </summary>
    internal static class HardwareShortNames
    {
        private static readonly Regex Marks = new Regex(@"\((?:R|TM|C)\)|[®™©]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Parenthesized = new Regex(@"\([^)]*\)", RegexOptions.CultureInvariant);
        private static readonly Regex CpuSuffix = new Regex(@"\s+(?:CPU\s+)?@.*$|\s+(?:w/|with)\s+Radeon.*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex CoreCount = new Regex(@"^(?:\d+|Dual|Quad|Six|Eight|Ten|Twelve|Sixteen)-Cores?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Ordinal = new Regex(@"^\d+(?:st|nd|rd|th)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex IntelCoreModel = new Regex(@"^i\d-", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Gpu(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var tokens = Tokens(name);
            while (tokens.Count > 1 && Is(tokens[0], "NVIDIA", "GeForce", "AMD", "ATI", "Intel")) tokens.RemoveAt(0);
            // "Radeon RX 9070 XT" is known as "RX 9070 XT"; an integrated "Radeon 890M" keeps its brand.
            if (tokens.Count > 2 && Is(tokens[0], "Radeon") && tokens[1].StartsWith("RX", StringComparison.OrdinalIgnoreCase))
                tokens.RemoveAt(0);
            // "Arc 140V GPU", "UHD Graphics 770": the word only stays when it is all that names the model.
            var model = tokens.Where(token => !Is(token, "Graphics", "GPU")).ToList();
            return Join(model.Count >= 2 ? model : tokens, name);
        }

        public static string Cpu(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var tokens = Tokens(CpuSuffix.Replace(name, ""));
            if (tokens.Any(token => token.Any(char.IsDigit)))
                tokens.RemoveAll(token => Is(token, "CPU", "Processor") || CoreCount.IsMatch(token));
            // Windows names 12th to 14th generation Core CPUs "13th Gen Intel(R) Core(TM) i7-13700K".
            if (tokens.Count > 2 && Ordinal.IsMatch(tokens[0]) && Is(tokens[1], "Gen")) tokens.RemoveRange(0, 2);
            while (tokens.Count > 1 && Is(tokens[0], "Intel", "AMD")) tokens.RemoveAt(0);
            // "Core i5-14600K" is "i5-14600K", "Core Ultra 7 258V" is "Ultra 7 258V".
            if (tokens.Count > 1 && Is(tokens[0], "Core") && (Is(tokens[1], "Ultra") || IntelCoreModel.IsMatch(tokens[1])))
                tokens.RemoveAt(0);
            // "Ryzen 7 9800X3D" is "R7 9800X3D", "Ryzen AI 9 HX 370" is "AI 9 HX 370".
            if (tokens.Count > 2 && Is(tokens[0], "Ryzen"))
            {
                if (tokens[1].Length == 1 && char.IsDigit(tokens[1][0]))
                {
                    tokens[1] = "R" + tokens[1];
                    tokens.RemoveAt(0);
                }
                else if (Is(tokens[1], "AI")) tokens.RemoveAt(0);
            }
            return Join(tokens, name);
        }

        private static List<string> Tokens(string name) => Parenthesized.Replace(Marks.Replace(name, " "), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        private static string Join(List<string> tokens, string name) => tokens.Count == 0 ? name.Trim() : string.Join(" ", tokens);

        private static bool Is(string token, params string[] words)
            => words.Any(word => string.Equals(token, word, StringComparison.OrdinalIgnoreCase));
    }
}
