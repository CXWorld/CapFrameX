using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    // The app's theme draws a halo around a slider's thumb while the pointer is over it or it has the
    // focus: 48 px wide around the 18 px thumb, so 15 px past either end of the slider. The inspector's
    // scroll area ended 6 px right of its fields and at their left edge, and cut the halo off there:
    // at 100 % tile opacity the thumb's circle had a flat right side.
    [STATestClass]
    [DoNotParallelize]
    public class OverlayInspectorSliderTest
    {
        [TestMethod]
        public void NoSliderThumbOfTheInspectorIsCutOffAtEitherEnd()
        {
            var cut = new List<string>();
            int sliders = 0;

            // Every tile kind with a slider of its own, on a canvas.
            var canvas = new OsdDesignDocument { Name = "Sliders", Width = 640, CanvasHeight = 640, IsCanvas = true, Padding = 12, Gap = 8, GridSize = 8 };
            canvas.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS", X = 0, Y = 0, FrameWidth = 160, FrameHeight = 80 });
            canvas.Tiles.Add(new OsdDesignTile { Kind = "bar", Label = "GPU", Key = "gpuLoad", Unit = "%", Minimum = 0, Maximum = 100, X = 168, Y = 0, FrameWidth = 240, FrameHeight = 56 });
            canvas.Tiles.Add(new OsdDesignTile
            {
                Kind = "icon", Label = "Icon", Key = null, Glyph = "", Family = "Segoe MDL2 Assets", Padding = 0, X = 416, Y = 0, FrameWidth = 40, FrameHeight = 40
            });
            canvas.Tiles.Add(new OsdDesignTile { Kind = "classicRows", Label = "Rows", Key = null, Height = 160, X = 0, Y = 88, FrameWidth = 320, FrameHeight = 160 });
            canvas.Tiles.Add(new OsdDesignTile
            {
                Kind = "chart", Label = "FRAME TIME", Key = "frametimes", Unit = "ms", X = 0, Y = 256, FrameWidth = 600, FrameHeight = 120,
                ChartLines = new List<OsdChartLine>
                {
                    new OsdChartLine { Series = "frametimes", Kind = "value" },
                    new OsdChartLine { Series = "frametimes", Kind = "stutter" }
                }
            });
            using (var host = new ThemedEditor(canvas))
            {
                foreach (var tile in host.Editor.Document.Tiles.ToArray())
                {
                    Assert.IsTrue(host.Editor.SelectElement(tile));
                    sliders += Check(host, 0, tile.Kind + " tile", cut);
                }
                sliders += Check(host, 1, "Layout tab", cut);
                sliders += Check(host, 2, "Style tab", cut);
            }

            // A flow design sizes a chart by a height slider.
            var flow = new OsdDesignDocument { Name = "Flow", Width = 320, Columns = 2, Padding = 12, Gap = 8 };
            flow.Tiles.Add(new OsdDesignTile { Kind = "chart", Label = "FRAME TIME", Key = "frametimes", Unit = "ms", ColumnSpan = 2 });
            using (var host = new ThemedEditor(flow))
            {
                Assert.IsTrue(host.Editor.SelectElement(host.Editor.Document.Tiles[0]));
                sliders += Check(host, 0, "chart tile of a flow design", cut);
            }

            Assert.IsTrue(sliders >= 11, $"Only {sliders} sliders were found: the row scale, a stutter factor, value size, height, icon size, bar " +
                "thickness, the four of the Layout tab and the tile opacity are expected.");
            Assert.AreEqual(0, cut.Count, "Thumbs that are cut off:" + Environment.NewLine + string.Join(Environment.NewLine, cut));
        }

        // Moves every visible slider of an inspector tab to both ends and reports where an ancestor clips
        // a part of its thumb, the halo included. Returns the number of sliders it moved.
        private static int Check(ThemedEditor host, int tab, string where, List<string> cut)
        {
            var tabs = (TabControl)host.Editor.FindName("InspectorTabs");
            tabs.SelectedIndex = tab;
            host.Settle();
            foreach (var expander in Descendants<Expander>(tabs)) expander.IsExpanded = true;
            host.Settle();
            var sliders = Descendants<Slider>(tabs).Where(slider => slider.IsVisible && slider.ActualWidth > 0).ToArray();
            foreach (var slider in sliders)
            {
                string name = where + ", " + (BindingOperations.GetBinding(slider, RangeBase.ValueProperty)?.Path?.Path ?? slider.Name);
                double value = slider.Value;
                foreach (double end in new[] { slider.Minimum, slider.Maximum })
                {
                    slider.SetCurrentValue(RangeBase.ValueProperty, end);
                    host.Window.UpdateLayout();
                    string clipped = ClippedPart(slider);
                    if (clipped != null) cut.Add($"{name} at {(end == slider.Minimum ? "its minimum" : "its maximum")}: {clipped}");
                }
                slider.SetCurrentValue(RangeBase.ValueProperty, value);
                host.Window.UpdateLayout();
            }
            return sliders.Length;
        }

        // The first part of the slider's thumb that an ancestor cuts off, up to the scroll area that holds
        // the slider; null if every part is whole. A part counts with its layout rectangle: the theme
        // scales the focus halo from nothing, and at rest that would hide what it needs.
        private static string ClippedPart(Slider slider)
        {
            var thumb = Descendants<Thumb>(slider).Single();
            foreach (var part in Descendants<FrameworkElement>(thumb).Where(part => part.Visibility == Visibility.Visible && part.RenderSize.Width > 0))
            {
                var parent = (Visual)VisualTreeHelper.GetParent(part);
                var inParent = new Rect((Point)VisualTreeHelper.GetOffset(part), part.RenderSize);
                for (DependencyObject node = parent; node != null; node = VisualTreeHelper.GetParent(node))
                {
                    if (node is not Visual ancestor) continue;
                    var clip = VisualTreeHelper.GetClip(ancestor);
                    if (clip != null)
                    {
                        Rect bounds = parent.TransformToAncestor(ancestor).TransformBounds(inParent), visible = clip.Bounds;
                        const double tolerance = .5;
                        if (bounds.Left < visible.Left - tolerance || bounds.Right > visible.Right + tolerance)
                        {
                            string type = ancestor.GetType().Name + ((ancestor as FrameworkElement)?.Name is { Length: > 0 } named ? " '" + named + "'" : "");
                            return $"{part.GetType().Name} '{part.Name}' is cut off by {type}: {Math.Max(0, visible.Left - bounds.Left):0.#} px at the left, " +
                                $"{Math.Max(0, bounds.Right - visible.Right):0.#} px at the right";
                        }
                    }
                    if (ancestor is ScrollContentPresenter) break;
                }
            }
            return null;
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        // The editor in a window with the app's theme, which is where the slider and its halo come from.
        private sealed class ThemedEditor : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }

            public ThemedEditor(OsdDesignDocument document)
            {
                var host = new Border();
                TransitionAssist.SetDisableTransitions(host, true);
                host.Resources.MergedDictionaries.Add(new CustomColorTheme { BaseTheme = BaseTheme.Dark, PrimaryColor = Color.FromRgb(2, 113, 249), SecondaryColor = Colors.Lime });
                foreach (string resource in new[] { "MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml", "CapFrameX.View;component/Styles/CxTextFields.xaml" })
                    host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + resource) });
                Editor = new OsdEditorControl();
                Editor.SetValue(ExpanderAssist.HorizontalHeaderPaddingProperty, new Thickness(0, 12, 0, 12));
                Editor.SetTemplateJson(document.ToJson());
                host.Child = Editor;
                Window = new Window
                {
                    Content = host, Width = 1280, Height = 960, WindowStyle = WindowStyle.None, ShowActivated = false,
                    ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                Window.Show();
                Settle();
            }

            public void Settle()
            {
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start();
                Dispatcher.PushFrame(frame);
                Window.UpdateLayout();
            }

            public void Dispose()
            {
                Window.Close();
                Editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Window.Content = null;
            }
        }
    }
}
