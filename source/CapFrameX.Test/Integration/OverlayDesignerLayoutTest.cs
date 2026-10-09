using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerLayoutTest
    {
        [TestMethod]
        [DataRow(BaseTheme.Dark, 1080, 560)]
        [DataRow(BaseTheme.Dark, 1280, 820)]
        [DataRow(BaseTheme.Light, 1080, 560)]
        [DataRow(BaseTheme.Light, 1280, 820)]
        public void MaterialDesignThemeKeepsAllSidebarTabsVisible(BaseTheme baseTheme, int width, int height)
        {
            // Use the same defaults and brush bindings as the CapFrameX host. The
            // standalone editor's system theme does not expose this regression.
            var host = new Border();
            host.Resources.MergedDictionaries.Add(new CustomColorTheme
            {
                BaseTheme = baseTheme,
                PrimaryColor = Color.FromRgb(2, 113, 249),
                SecondaryColor = Colors.Lime
            });
            host.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml")
            });
            var editor = new OsdEditorControl();
            editor.SetResourceReference(OsdEditorControl.SurfaceBrushProperty, "MaterialDesign.Brush.TextBox.FilledBackground");
            editor.SetResourceReference(OsdEditorControl.AccentBrushProperty, "MaterialDesign.Brush.Primary");
            editor.SetResourceReference(Control.BackgroundProperty, "MaterialDesign.Brush.Card.Background");
            editor.SetResourceReference(Control.ForegroundProperty, "MaterialDesign.Brush.Foreground");
            editor.Sources = new[]
            {
                new OsdTelemetrySource { Key = "sensor:gpu/load", Name = "GPU load", Category = "GPU", Unit = "%" },
                new OsdTelemetrySource { Key = "sensor:cpu/temperature", Name = "CPU temperature", Category = "CPU", Unit = "°C" }
            };
            host.Child = editor;
            Arrange(host, width, height);

            var sidebars = Descendants<TabControl>(editor).ToArray();
            Assert.AreEqual(2, sidebars.Length, "The palette and inspector must both be realized.");
            CollectionAssert.AreEquivalent(new[] { "Add", "Tiles", "Sources", "Tile", "Layout", "Style" },
                sidebars.SelectMany(tabs => tabs.Items.Cast<TabItem>()).Select(tab => tab.Header.ToString()).ToArray());

            foreach (var tabs in sidebars)
            {
                foreach (TabItem selected in tabs.Items)
                {
                    tabs.SelectedItem = selected;
                    Arrange(host, width, height);
                    foreach (TabItem tab in tabs.Items)
                    {
                        AssertHeaderVisible(tabs, tab);
                    }
                }
            }

            var sources = (TabItem)editor.FindName("SourcesTab");
            sources.IsSelected = true;
            Arrange(host, width, height);
            var sourceList = (ListBox)editor.FindName("SourceList");
            Assert.AreEqual(2, sourceList.Items.Count, "The Sources tab must retain the supplied catalog.");
            Assert.IsTrue(sourceList.ActualWidth > 0 && sourceList.ActualHeight > 0,
                "Selecting Sources must expose a usable source list.");
            Assert.IsTrue(Descendants<ListBox>(sidebars.Single(tabs => tabs.Items.Contains(sources))).Contains(sourceList),
                "The source list must be in the selected tab's visual tree.");
            host.Child = null;
        }

        private static void AssertHeaderVisible(TabControl tabs, TabItem tab)
        {
            Assert.AreEqual(Visibility.Visible, tab.Visibility, $"The {tab.Header} tab is hidden.");
            Assert.IsTrue(tab.ActualWidth > 0 && tab.ActualHeight > 0, $"The {tab.Header} tab has no visible area.");
            Rect bounds = tab.TransformToAncestor(tabs).TransformBounds(new Rect(tab.RenderSize));
            Assert.IsTrue(bounds.Left >= -0.5 && bounds.Right <= tabs.ActualWidth + 0.5,
                $"The {tab.Header} tab is clipped horizontally: header {bounds.Left:F1}–{bounds.Right:F1}, sidebar width {tabs.ActualWidth:F1}.");
            Assert.IsTrue(bounds.Top >= -0.5 && bounds.Bottom <= tabs.ActualHeight + 0.5,
                $"The {tab.Header} tab is clipped vertically.");

            var header = Descendants<ContentPresenter>(tab).FirstOrDefault(presenter => Equals(presenter.Content, tab.Header));
            Assert.IsNotNull(header, $"The {tab.Header} tab label was not realized.");
            Assert.IsTrue(header.ActualWidth + header.Margin.Left + header.Margin.Right + 0.5 >= header.DesiredSize.Width,
                $"The {tab.Header} tab label does not fit its header.");
        }

        private static void Arrange(FrameworkElement host, int width, int height)
        {
            host.Measure(new Size(width, height));
            host.Arrange(new Rect(0, 0, width, height));
            host.UpdateLayout();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match)
                {
                    yield return match;
                }
                foreach (T descendant in Descendants<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
