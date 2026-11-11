using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CapFrameX.CaptureHelper
{
    /// <summary>
    /// Frame times over the capture time. Per pixel column it draws the shortest and the longest
    /// frame, so long captures stay fast to draw and still show every spike.
    /// </summary>
    public sealed class FrametimeGraph : FrameworkElement
    {
        private const double LabelSize = 11;
        private static readonly Thickness PlotMargin = new Thickness(48, 26, 16, 26);
        private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x5F, 0x66, 0x70)));
        private static readonly Pen LinePen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x02, 0x71, 0xF9))), 1));
        private static readonly Pen GridPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xED))), 1));
        private static readonly Typeface LabelFont = new Typeface("Segoe UI");

        private double[] _frametimes = Array.Empty<double>();
        private double[] _startTimes = Array.Empty<double>();
        private double _duration;
        private string _message = "Select a capture on the left.";

        public void SetFrametimes(double[] frametimes)
        {
            _frametimes = frametimes;
            _startTimes = new double[frametimes.Length];
            double time = 0;
            for (int i = 0; i < frametimes.Length; i++)
            {
                _startTimes[i] = time;
                time += frametimes[i] / 1000.0;
            }
            _duration = time;
            _message = null;
            InvalidateVisual();
        }

        public void Clear(string message)
        {
            _frametimes = Array.Empty<double>();
            _startTimes = Array.Empty<double>();
            _duration = 0;
            _message = message;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(RenderSize);
            dc.DrawRectangle(Brushes.White, null, bounds);
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            if (_frametimes.Length < 2 || _duration <= 0)
            {
                if (!string.IsNullOrEmpty(_message))
                {
                    var text = Text(_message, pixelsPerDip, 13);
                    dc.DrawText(text, new Point((bounds.Width - text.Width) / 2, (bounds.Height - text.Height) / 2));
                }
                return;
            }

            var plot = new Rect(PlotMargin.Left, PlotMargin.Top,
                Math.Max(0, bounds.Width - PlotMargin.Left - PlotMargin.Right),
                Math.Max(0, bounds.Height - PlotMargin.Top - PlotMargin.Bottom));
            if (plot.Width < 20 || plot.Height < 20) return;

            double yStep = NiceStep(_frametimes.Max() / 5);
            double yMax = Math.Ceiling(_frametimes.Max() / yStep) * yStep;
            double xStep = NiceStep(_duration / Math.Max(2, plot.Width / 90));

            for (int k = 0; k * yStep <= yMax * (1 + 1e-9); k++)
            {
                double value = k * yStep;
                double y = Math.Round(plot.Bottom - value / yMax * plot.Height) + 0.5;
                dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
                var label = Text(value.ToString("0.##", CultureInfo.CurrentCulture), pixelsPerDip, LabelSize);
                dc.DrawText(label, new Point(plot.Left - 6 - label.Width, y - label.Height / 2));
            }
            for (int k = 0; k * xStep <= _duration * (1 + 1e-9); k++)
            {
                double seconds = k * xStep;
                double x = Math.Round(plot.Left + seconds / _duration * plot.Width) + 0.5;
                dc.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
                var label = Text(seconds.ToString("0.##", CultureInfo.CurrentCulture) + " s", pixelsPerDip, LabelSize);
                dc.DrawText(label, new Point(x - label.Width / 2, plot.Bottom + 5));
            }
            var unit = Text("Frametime (ms)", pixelsPerDip, LabelSize);
            dc.DrawText(unit, new Point(plot.Left, plot.Top - unit.Height - 9));

            dc.PushClip(new RectangleGeometry(plot));
            dc.DrawGeometry(null, LinePen, BuildLine(plot, yMax));
            dc.Pop();
        }

        private Geometry BuildLine(Rect plot, double yMax)
        {
            var points = new List<Point>();
            int columns = Math.Max(1, (int)Math.Ceiling(plot.Width));
            int column = -1, shortest = 0, longest = 0;
            for (int i = 0; i < _frametimes.Length; i++)
            {
                int frameColumn = Math.Min(columns - 1, (int)(_startTimes[i] / _duration * columns));
                if (frameColumn != column)
                {
                    if (column >= 0) AddColumn(points, plot, yMax, shortest, longest);
                    column = frameColumn;
                    shortest = longest = i;
                }
                else
                {
                    if (_frametimes[i] < _frametimes[shortest]) shortest = i;
                    if (_frametimes[i] > _frametimes[longest]) longest = i;
                }
            }
            AddColumn(points, plot, yMax, shortest, longest);

            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], false, false);
                context.PolyLineTo(points.Skip(1).ToList(), true, false);
            }
            geometry.Freeze();
            return geometry;
        }

        // The two frames of a column in the order they occurred, so the line keeps the time order.
        private void AddColumn(List<Point> points, Rect plot, double yMax, int shortest, int longest)
        {
            int first = Math.Min(shortest, longest), second = Math.Max(shortest, longest);
            points.Add(ToPoint(plot, yMax, first));
            if (second != first) points.Add(ToPoint(plot, yMax, second));
        }

        private Point ToPoint(Rect plot, double yMax, int frame) => new Point(
            plot.Left + _startTimes[frame] / _duration * plot.Width,
            plot.Bottom - _frametimes[frame] / yMax * plot.Height);

        // 1, 2 or 5 times a power of ten, at least the given step.
        private static double NiceStep(double rawStep)
        {
            if (!(rawStep > 0)) return 1;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double fraction = rawStep / magnitude;
            return (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * magnitude;
        }

        private static FormattedText Text(string text, double pixelsPerDip, double size) =>
            new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFont, size, LabelBrush, pixelsPerDip);

        private static T Frozen<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }
    }
}
