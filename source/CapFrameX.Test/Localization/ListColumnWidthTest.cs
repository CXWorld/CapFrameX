using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Localization
{
    /// <summary>
    /// A DataGrid column without a width sizes to the rows on screen and widens while scrolling, which
    /// pushed the last columns of these lists out of view with longer translations (issue #448). Their
    /// text columns have fixed or star widths and trim their text instead.
    /// </summary>
    [TestClass]
    public class ListColumnWidthTest
    {
        private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        [DataTestMethod]
        [DataRow("OverlayView.xaml", "OverlayItemDataGrid")]
        [DataRow("SensorView.xaml", "SensorItemDataGrid")]
        [DataRow("SensorView.xaml", "SensorStatisticsDataGrid")]
        public void TextColumns_HaveAFixedOrStarWidth(string view, string gridName)
        {
            var grid = LoadView(view).Descendants(Wpf + "DataGrid").Single(element => (string)element.Attribute(Xaml + "Name") == gridName);
            var columns = grid.Element(Wpf + "DataGrid.Columns").Elements()
                .Where(column => column.Name == Wpf + "DataGridTextColumn" || column.Name == Wpf + "DataGridTemplateColumn")
                .ToList();
            Assert.IsTrue(columns.Count > 0, gridName + " has no text columns.");

            foreach (var column in columns)
            {
                string width = (string)column.Attribute("Width");
                Assert.IsFalse(string.IsNullOrEmpty(width) || width == "Auto" || width.StartsWith("SizeTo", StringComparison.Ordinal),
                    $"{gridName}: the column {(string)column.Attribute("Header")} sizes to its content.");
            }
            Assert.AreNotEqual("Visible", (string)grid.Attribute("ScrollViewer.HorizontalScrollBarVisibility"),
                gridName + " fits its columns and needs no permanent horizontal scroll bar.");
        }

        private static XDocument LoadView(string view)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CapFrameX.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory);
            return XDocument.Load(Path.Combine(directory.FullName, "source", "CapFrameX.View", view));
        }
    }
}
