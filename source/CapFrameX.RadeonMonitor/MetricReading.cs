using System.Globalization;

namespace CapFrameX.RadeonMonitor
{
    internal enum MetricValueKind
    {
        Numeric,
        PcieGeneration,
        PcieWidth
    }

    internal sealed record MetricReading(
        string Group,
        string Name,
        string CurrentValue,
        string Unit,
        string Raw,
        double? NumericValue = null,
        int DecimalPlaces = 0,
        MetricValueKind ValueKind = MetricValueKind.Numeric)
    {
        public string MinimumValue { get; init; } = "—";

        public string MaximumValue { get; init; } = "—";

        public string AverageValue { get; init; } = "—";

        public string FormatStatisticValue(double value, bool isAverage)
        {
            int decimalPlaces = isAverage ? Math.Max(1, DecimalPlaces) : DecimalPlaces;
            string formattedValue = value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);

            return ValueKind switch
            {
                MetricValueKind.PcieGeneration => $"Gen {formattedValue}",
                MetricValueKind.PcieWidth => $"x{formattedValue}",
                _ => formattedValue
            };
        }
    }
}
