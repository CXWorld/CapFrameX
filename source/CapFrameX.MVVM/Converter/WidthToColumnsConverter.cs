using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
    /// <summary>
    /// Picks the column count of a grid from the width it has: the first of <see cref="Choices"/>
    /// whose columns are at least <see cref="MinColumnWidth"/> wide, else the last choice. Bound to
    /// the grid's own ActualWidth, a row of tiles wraps into an even layout (4, then 2, then 1)
    /// on a narrow window instead of cutting off its labels.
    /// </summary>
    public sealed class WidthToColumnsConverter : IValueConverter
    {
        private int[] _choices = { 1 };
        private string _choicesText = "1";

        public double MinColumnWidth { get; set; } = 100;

        /// <summary>
        /// The column counts to try, from the widest layout down, e.g. "4,2,1".
        /// </summary>
        public string Choices
        {
            get => _choicesText;
            set
            {
                var parsed = (value ?? string.Empty)
                    .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) ? count : 0)
                    .Where(count => count > 0)
                    .ToArray();
                _choices = parsed.Length > 0 ? parsed : new[] { 1 };
                _choicesText = value;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => GetColumns(value is double width ? width : double.NaN);

        public int GetColumns(double width)
        {
            // Before the first layout pass the width is 0: keep the widest layout, which is the
            // one every larger window shows.
            if (double.IsNaN(width) || width <= 0)
                return _choices[0];

            foreach (var columns in _choices)
            {
                if (width / columns >= MinColumnWidth)
                    return columns;
            }

            return _choices[_choices.Length - 1];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
