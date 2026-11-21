using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapFrameX.OSD.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    // A chart's graph line width and its outline width are empty until a width is typed in, and the
    // renderer then draws one pixel. The empty fields looked as if no width were in use; they show that
    // pixel in the field's design units as a placeholder, as an empty color field shows its default.
    [STATestClass]
    [DoNotParallelize]
    public class OverlayChartWidthFieldTest
    {
        [TestMethod]
        public void EmptyChartWidthFieldsShowTheOnePixelTheRendererDraws()
        {
            var document = new OsdDesignDocument { Name = "Widths", Width = 640, CanvasHeight = 320, IsCanvas = true, Padding = 12, Gap = 8, GridSize = 8, Scale = 1.5 };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "chart", Label = "FRAME RATE", Key = "framerates", Unit = "FPS", X = 0, Y = 0, FrameWidth = 400, FrameHeight = 120, ChartBorderWidth = 1.5
            });
            using var host = new OverlayInspectorSliderTest.ThemedEditor(document);
            var chart = host.Editor.Document.Tiles.Single();
            Assert.IsTrue(host.Editor.SelectElement(chart));
            ((TabControl)host.Editor.FindName("InspectorTabs")).SelectedIndex = 0;
            host.Settle();
            foreach (var expander in Descendants<Expander>(host.Editor)) expander.IsExpanded = true;
            host.Settle();

            var graph = (TextBox)host.Editor.FindName("ChartGraphLineWidthField");
            var graphDefault = (TextBlock)host.Editor.FindName("ChartGraphLineWidthDefault");
            var outline = (TextBox)host.Editor.FindName("ChartLineWidthField");
            var outlineDefault = (TextBlock)host.Editor.FindName("ChartLineWidthDefault");
            Assert.IsTrue(graph.IsVisible && outline.IsVisible, "Both width fields of a chart are on screen.");

            // One pixel at scale 1.5 is 0.67 design units; the outline's width of its own hides its placeholder.
            Assert.AreEqual("", graph.Text);
            Assert.IsTrue(graphDefault.IsVisible, "An empty graph line width shows its default.");
            Assert.AreEqual("0.67 (1 px)", graphDefault.Text);
            Assert.AreEqual("1.5", outline.Text);
            Assert.IsFalse(outlineDefault.IsVisible, "A width typed in replaces the placeholder.");

            // A width typed in reaches the tile and hides the placeholder; clearing it shows the default again.
            graph.Text = "2";
            graph.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            host.Settle();
            Assert.AreEqual(2.0, chart.ChartLineWidth);
            Assert.IsFalse(graphDefault.IsVisible);
            graph.Text = "";
            graph.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            outline.Text = "";
            outline.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            host.Settle();
            Assert.IsNull(chart.ChartLineWidth);
            Assert.IsNull(chart.ChartBorderWidth);
            Assert.IsTrue(graphDefault.IsVisible && outlineDefault.IsVisible, "Cleared fields show the default again.");
            Assert.AreEqual("0.67 (1 px)", outlineDefault.Text);

            // The placeholder follows the design's scale.
            host.Editor.Document.Scale = 1;
            host.Settle();
            Assert.AreEqual("1 (1 px)", graphDefault.Text);
            Assert.AreEqual("1 (1 px)", outlineDefault.Text);
        }

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }
    }
}
