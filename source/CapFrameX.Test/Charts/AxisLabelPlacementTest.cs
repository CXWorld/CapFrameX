using System;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Dtos;
using LiveCharts.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Separator = LiveCharts.Wpf.Separator;

namespace CapFrameX.Test.Charts
{
    /// <summary>
    /// The labels of an X axis lie between the plot area and the edge of the chart. LiveCharts
    /// placed them 4 px away from the plot area without reserving those pixels, so the labels of
    /// a bottom axis ended below the chart, where its clip cut them off (the metrics bar chart of
    /// the Analysis tab), or over the axis title (the L-shape chart).
    /// </summary>
    [TestClass]
    public class AxisLabelPlacementTest
    {
        private const double ChartWidth = 600;
        private const double ChartHeight = 250;
        private const double Tolerance = 0.01;

        private static readonly string[] ValueLabels = { "0", "25", "50", "75", "100", "125" };

        [TestMethod]
        public void BottomAxisLabelsEndInsideTheChart()
        {
            RunOnStaThread(() =>
            {
                var chart = CreateBarChart(CreateValueAxis());

                var plotBottom = chart.Model.DrawMargin.Top + chart.Model.DrawMargin.Height;
                foreach (var (label, top, bottom) in ValueLabelBounds(chart))
                {
                    Assert.IsTrue(top >= plotBottom - Tolerance,
                        $"Label {label} starts at {top:F2}, inside the plot area, which ends at {plotBottom:F2}.");
                    Assert.IsTrue(bottom <= ChartHeight + Tolerance,
                        $"Label {label} ends at {bottom:F2}, below the chart, which ends at {ChartHeight:F2}.");
                }
            });
        }

        [TestMethod]
        public void BottomAxisLabelsEndAboveTheAxisTitle()
        {
            RunOnStaThread(() =>
            {
                var axis = CreateValueAxis();
                axis.Title = "Percentiles";
                var chart = CreateBarChart(axis);

                var titleTop = axis.GetTileTop();
                Assert.IsTrue(titleTop + axis.GetLabelSize().Height <= ChartHeight + Tolerance,
                    "The axis title ends below the chart.");
                foreach (var (label, _, bottom) in ValueLabelBounds(chart))
                {
                    Assert.IsTrue(bottom <= titleTop + Tolerance,
                        $"Label {label} ends at {bottom:F2}, over the axis title, which starts at {titleTop:F2}.");
                }
            });
        }

        [TestMethod]
        public void TopAxisLabelsStartBelowTheAxisTitle()
        {
            RunOnStaThread(() =>
            {
                var axis = CreateValueAxis();
                axis.Title = "Percentiles";
                axis.Position = AxisPosition.RightTop;
                var chart = CreateBarChart(axis);

                var titleBottom = axis.GetTileTop() + axis.GetLabelSize().Height;
                var plotTop = chart.Model.DrawMargin.Top;
                foreach (var (label, top, bottom) in ValueLabelBounds(chart))
                {
                    Assert.IsTrue(top >= titleBottom - Tolerance,
                        $"Label {label} starts at {top:F2}, over the axis title, which ends at {titleBottom:F2}.");
                    Assert.IsTrue(bottom <= plotTop + Tolerance,
                        $"Label {label} ends at {bottom:F2}, inside the plot area, which starts at {plotTop:F2}.");
                }
            });
        }

        private static Axis CreateValueAxis()
        {
            return new Axis
            {
                FontSize = 14,
                MinValue = 0,
                MaxValue = 125,
                LabelFormatter = value => value.ToString(CultureInfo.InvariantCulture),
                Separator = new Separator { Step = 25 }
            };
        }

        /// <summary>
        /// The metrics bar chart of the Analysis tab: one row per metric, the values on the X axis.
        /// </summary>
        private static CartesianChart CreateBarChart(Axis valueAxis)
        {
            var chart = new CartesianChart
            {
                DisableAnimations = true,
                IsMocked = true,
                Series = new SeriesCollection
                {
                    new RowSeries { Values = new ChartValues<double> { 8.1, 91.5, 101.8, 120 } }
                }
            };

            chart.AxisX.Add(valueAxis);
            chart.AxisY.Add(new Axis
            {
                FontSize = 14,
                Labels = new[] { "Adaptive STDEV", "P0.2", "P1", "Average" },
                Separator = new Separator { Step = 1, IsEnabled = false }
            });

            chart.Model.ControlSize = new CoreSize(ChartWidth, ChartHeight);

            // The first update knows no plot area yet, so the Y axis reserves half a label below
            // it, which hid the missing room of the X axis labels. Every later update, as in the
            // app, computes the row height from the plot area of the update before.
            chart.Model.Updater.Run(false, true);
            chart.Model.Updater.Run(false, true);
            return chart;
        }

        private static (string Label, double Top, double Bottom)[] ValueLabelBounds(CartesianChart chart)
        {
            var labels = ((Canvas)chart.Content).Children.OfType<TextBlock>()
                .Where(block => ValueLabels.Contains(block.Text))
                .Select(block =>
                {
                    block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var top = Canvas.GetTop(block);
                    return (block.Text, top, top + block.DesiredSize.Height);
                })
                .ToArray();

            CollectionAssert.AreEquivalent(ValueLabels, labels.Select(label => label.Text).ToArray(),
                "The chart did not draw every value label.");
            return labels;
        }

        private static void RunOnStaThread(Action test)
        {
            Exception testException = null;
            var testThread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception exception)
                {
                    testException = exception;
                }
            });

            testThread.SetApartmentState(ApartmentState.STA);
            testThread.Start();

            Assert.IsTrue(testThread.Join(TimeSpan.FromSeconds(10)), "The WPF test thread did not finish.");

            if (testException != null)
            {
                ExceptionDispatchInfo.Capture(testException).Throw();
            }
        }
    }
}
