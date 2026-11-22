using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// The run history as the run history tile reads it: one text reading in the renderer's table format
    /// (the OSD core's RunHistory.h, version 1). Lines separated by '\n', fields by '\t', numbers in the
    /// invariant culture, "-" for a missing one:
    /// <c>RH1 enabled slots digits reference</c>, <c>C captions…</c>, one <c>R values… deviation outlier</c>
    /// per completed run, <c>A values…</c> for the aggregated result, <c>T result-label off-text</c>.
    /// </summary>
    public static class RunHistoryTablePayload
    {
        /// <summary>The source the tile reads, and its semantic key.</summary>
        public const string SourceIdentifier = "RunHistoryTable", SemanticKey = "runHistoryTable";
        /// <summary>The renderer reads text readings of at most this many UTF-8 bytes.</summary>
        public const int MaximumBytes = 2048;

        /// <param name="snapshot">The run history (IOverlayService.RunHistoryTable).</param>
        /// <param name="enabled">Whether runs are recorded (UseRunHistory).</param>
        /// <param name="digits">Decimals of FPS values (FpsValuesRoundingDigits).</param>
        /// <param name="translate">Overlay language texts (CxLang.TranslateOverlay).</param>
        public static string Build(RunHistorySnapshot snapshot, bool enabled, int digits, Func<string, string> translate)
        {
            snapshot ??= RunHistorySnapshot.Empty;
            translate ??= text => text;
            digits = Math.Clamp(digits, 0, 6);
            bool second = HasMetric(snapshot.SecondMetric), third = HasMetric(snapshot.ThirdMetric);
            var captions = new List<string> { Caption(EMetric.Average.GetShortDescription(), translate) };
            if (second) captions.Add(Caption(ShortName(snapshot.SecondMetric), translate));
            if (third) captions.Add(Caption(ShortName(snapshot.ThirdMetric), translate));
            int reference = snapshot.ReferenceMetric switch
            {
                "Second" => second ? 1 : -1,
                "Third" => third ? (second ? 2 : 1) : -1,
                _ => 0
            };

            var text = new StringBuilder();
            text.Append("RH1\t").Append(enabled ? '1' : '0').Append('\t')
                .Append(Math.Clamp(snapshot.Slots, 0, 20).ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(digits.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(reference.ToString(CultureInfo.InvariantCulture));
            text.Append("\nC");
            foreach (string caption in captions) text.Append('\t').Append(caption);
            foreach (var run in snapshot.Runs.Take(20))
            {
                text.Append("\nR");
                AppendValues(text, run, snapshot, second, third, digits);
                text.Append('\t').Append(reference < 0 ? "-" : Number(run.Deviation, 2))
                    .Append('\t').Append(run.IsOutlier ? '1' : '0');
            }
            if (snapshot.Result != null)
            {
                text.Append("\nA");
                AppendValues(text, snapshot.Result, snapshot, second, third, digits);
            }
            text.Append("\nT\t").Append(Clean(translate("Result:")).TrimEnd(':', '：', ' '))
                .Append('\t').Append(Clean(translate("Off")));
            string payload = text.ToString();
            // The renderer refuses the whole metrics update when one text is longer, which would empty
            // every tile of the design. The largest session takes about 1.2 KB, so only absurd texts
            // get here: the table then shows its session without runs rather than nothing at all.
            if (Encoding.UTF8.GetByteCount(payload) > MaximumBytes)
                payload = string.Join("\n", payload.Split('\n').Take(1).Append("C\t" + string.Join("\t",
                    captions.Select(caption => caption.Length > 12 ? caption.Substring(0, 12) : caption))));
            return payload;
        }

        // A run measured in other metrics than the history shows now (the metric was changed during a
        // session) leaves those cells empty rather than putting its values under another caption.
        private static void AppendValues(StringBuilder text, RunHistoryRun run, RunHistorySnapshot snapshot,
            bool second, bool third, int digits)
        {
            text.Append('\t').Append(Number(run.Average, digits));
            if (second) text.Append('\t').Append(run.SecondMetric == snapshot.SecondMetric ? Number(run.Second, digits) : "-");
            if (third) text.Append('\t').Append(run.ThirdMetric == snapshot.ThirdMetric ? Number(run.Third, digits) : "-");
        }

        private static bool HasMetric(string metric)
            => !string.IsNullOrWhiteSpace(metric) && Enum.TryParse(metric, out EMetric value) && value != EMetric.None;

        private static string ShortName(string metric) => Enum.Parse<EMetric>(metric).GetShortDescription();

        private static string Caption(string caption, Func<string, string> translate)
        {
            string translated = Clean(translate(caption));
            return string.IsNullOrWhiteSpace(translated) ? caption : translated;
        }

        private static string Number(double value, int digits)
            => double.IsFinite(value) ? value.ToString("F" + digits, CultureInfo.InvariantCulture) : "-";

        // Tabs and line breaks separate the fields and lines of the table.
        private static string Clean(string text)
            => new string((text ?? string.Empty).Select(ch => ch == '\t' || ch == '\r' || ch == '\n' ? ' ' : ch).ToArray()).Trim();
    }
}
