using System;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// Stacks fixed-width cards into three columns when the width allows it, otherwise into two
    /// (or one). Each card names its column for both layouts, so a narrower window can
    /// redistribute the cards instead of wrapping whole columns below each other. Cards keep
    /// their document order within a column; one <see cref="Spacing"/> separates cards both
    /// horizontally and vertically.
    /// </summary>
    public class OsdColumnsPanel : Panel
    {
        public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(
            nameof(ColumnWidth), typeof(double), typeof(OsdColumnsPanel),
            new FrameworkPropertyMetadata(440d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
            nameof(Spacing), typeof(double), typeof(OsdColumnsPanel),
            new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ThreeColumnIndexProperty = DependencyProperty.RegisterAttached(
            "ThreeColumnIndex", typeof(int), typeof(OsdColumnsPanel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        public static readonly DependencyProperty TwoColumnIndexProperty = DependencyProperty.RegisterAttached(
            "TwoColumnIndex", typeof(int), typeof(OsdColumnsPanel),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

        public double ColumnWidth
        {
            get => (double)GetValue(ColumnWidthProperty);
            set => SetValue(ColumnWidthProperty, value);
        }

        public double Spacing
        {
            get => (double)GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public static int GetThreeColumnIndex(DependencyObject element) => (int)element.GetValue(ThreeColumnIndexProperty);
        public static void SetThreeColumnIndex(DependencyObject element, int value) => element.SetValue(ThreeColumnIndexProperty, value);
        public static int GetTwoColumnIndex(DependencyObject element) => (int)element.GetValue(TwoColumnIndexProperty);
        public static void SetTwoColumnIndex(DependencyObject element, int value) => element.SetValue(TwoColumnIndexProperty, value);

        protected override Size MeasureOverride(Size availableSize)
        {
            int columns = ColumnCount(availableSize.Width);
            var heights = new double[columns];
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(ColumnWidth, double.PositiveInfinity));
                if (child.Visibility == Visibility.Collapsed)
                    continue;

                int column = ColumnOf(child, columns);
                heights[column] += (heights[column] > 0 ? Spacing : 0) + child.DesiredSize.Height;
            }

            double height = 0;
            foreach (double h in heights)
                height = Math.Max(height, h);
            return new Size(TotalWidth(columns), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int columns = ColumnCount(finalSize.Width);
            var tops = new double[columns];
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed)
                    continue;

                int column = ColumnOf(child, columns);
                if (tops[column] > 0)
                    tops[column] += Spacing;
                child.Arrange(new Rect(column * (ColumnWidth + Spacing), tops[column], ColumnWidth, child.DesiredSize.Height));
                tops[column] += child.DesiredSize.Height;
            }

            return new Size(TotalWidth(columns), finalSize.Height);
        }

        private int ColumnCount(double width)
        {
            if (double.IsInfinity(width) || width >= TotalWidth(3))
                return 3;
            return width >= TotalWidth(2) ? 2 : 1;
        }

        private double TotalWidth(int columns) => columns * ColumnWidth + (columns - 1) * Spacing;

        private static int ColumnOf(UIElement child, int columns)
        {
            int column = columns == 3 ? GetThreeColumnIndex(child)
                : columns == 2 ? GetTwoColumnIndex(child)
                : 0;
            return Math.Max(0, Math.Min(columns - 1, column));
        }
    }
}
