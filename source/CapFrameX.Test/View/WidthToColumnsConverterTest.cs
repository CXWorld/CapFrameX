using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CapFrameX.MVVM.Converter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.View
{
    /// <summary>
    /// A row of tiles wraps into an even layout when its columns would get narrower than their
    /// minimum: the info page's GPU tiles at 1080p and 150 % (about 364 wide) become 2x2.
    /// </summary>
    [STATestClass]
    public class WidthToColumnsConverterTest
    {
        private static WidthToColumnsConverter FourTiles() =>
            new WidthToColumnsConverter { MinColumnWidth = 110, Choices = "4,2,1" };

        [TestMethod]
        public void GetColumns_TakesTheWidestChoiceWhoseColumnsAreWideEnough()
        {
            var converter = FourTiles();

            Assert.AreEqual(4, converter.GetColumns(525));
            Assert.AreEqual(4, converter.GetColumns(440));
            Assert.AreEqual(2, converter.GetColumns(439));
            Assert.AreEqual(2, converter.GetColumns(364));
            Assert.AreEqual(1, converter.GetColumns(150));
        }

        [TestMethod]
        public void GetColumns_KeepsTheLastChoiceBelowEveryMinimum()
        {
            Assert.AreEqual(1, FourTiles().GetColumns(40));
        }

        [TestMethod]
        public void GetColumns_KeepsTheWidestLayoutBeforeTheFirstLayoutPass()
        {
            var converter = FourTiles();

            Assert.AreEqual(4, converter.GetColumns(0));
            Assert.AreEqual(4, converter.GetColumns(double.NaN));
        }

        [TestMethod]
        public void Choices_IgnoresInvalidEntriesAndFallsBackToOneColumn()
        {
            var converter = new WidthToColumnsConverter { MinColumnWidth = 100, Choices = "x, 3, 0, -2" };
            Assert.AreEqual(3, converter.GetColumns(300));
            Assert.AreEqual(3, converter.GetColumns(120), "Below every minimum the last valid choice applies.");

            converter.Choices = string.Empty;
            Assert.AreEqual(1, converter.GetColumns(1000));
        }

        [TestMethod]
        public void UniformGrid_BoundToItsOwnWidth_WrapsItsTilesWhenNarrow()
        {
            var grid = new UniformGrid();
            for (int i = 0; i < 4; i++)
                grid.Children.Add(new Border { Height = 40 });
            BindingOperations.SetBinding(grid, UniformGrid.ColumnsProperty, new Binding(nameof(FrameworkElement.ActualWidth))
            {
                RelativeSource = RelativeSource.Self,
                Converter = FourTiles()
            });

            Layout(grid, 364);
            Assert.AreEqual(2, grid.Columns);
            Assert.AreEqual(80, grid.ActualHeight, 0.5, "Two rows of tiles.");

            Layout(grid, 600);
            Assert.AreEqual(4, grid.Columns);
            Assert.AreEqual(40, grid.ActualHeight, 0.5, "One row of tiles.");
        }

        private static void Layout(FrameworkElement element, double width)
        {
            // The binding follows ActualWidth, which a pass sets; the second pass applies the columns.
            for (int pass = 0; pass < 3; pass++)
            {
                element.Measure(new Size(width, double.PositiveInfinity));
                element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
                element.UpdateLayout();
            }
        }
    }
}
