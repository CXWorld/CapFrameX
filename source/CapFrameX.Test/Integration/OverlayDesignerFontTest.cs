using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    /// <summary>The overlay editor offers a font for the design and for every tile that draws text.</summary>
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerFontTest
    {
        [TestMethod]
        public void TextFontsCarryOverBetweenTextKindsAndEveryTextTileOffersTheFontPicker()
        {
            using var host = new EditorHost();
            var tile = host.Editor.SelectedElement;
            var typography = (Expander)host.Editor.FindName("TypographyExpander");
            Assert.AreEqual("Consolas", tile.Family);
            Assert.AreEqual(Visibility.Visible, typography.Visibility);

            foreach (var kind in new[] { "metric", "bar", "gauge", "chart", "classicRows", "text" })
            {
                tile.Kind = kind;
                Settle();
                Assert.AreEqual("Consolas", tile.Family, $"A text font must survive the switch to {kind}.");
                Assert.AreEqual(Visibility.Visible, typography.Visibility, $"A {kind} tile must offer its font.");
            }

            tile.Kind = "icon";
            Settle();
            Assert.AreNotEqual("Consolas", tile.Family, "A text font draws no icon glyphs.");
            Assert.AreEqual(Visibility.Collapsed, typography.Visibility, "An icon takes its font from the icon picker.");

            tile.Kind = "text";
            Settle();
            Assert.IsNull(tile.Family, "An icon font draws no text.");
        }

        [TestMethod]
        public void LayoutTabFontBecomesTheDesignThemeAndUndoRestoresIt()
        {
            using var host = new EditorHost();
            var picker = LogicalDescendants<ComboBox>(host.Editor).SingleOrDefault(combo =>
                BindingOperations.GetBindingExpression(combo, ComboBox.TextProperty)?.ParentBinding.Path.Path == "Document.FontFamily");
            Assert.IsNotNull(picker, "The layout tab must offer the design font.");
            Assert.IsTrue(picker.IsEditable, "Fonts that are not listed can be typed.");
            string before = host.Editor.GetTemplateJson();

            host.Editor.Document.FontFamily = "Bahnschrift";
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual("Bahnschrift", JObject.Parse(host.Editor.GetTemplateJson())["theme"]["fontFamily"].Value<string>());
            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual(before, host.Editor.GetTemplateJson());
        }

        private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is not DependencyObject dependency) continue;
                if (dependency is T match) yield return match;
                foreach (T descendant in LogicalDescendants<T>(dependency)) yield return descendant;
            }
        }

        private static void Settle(int milliseconds = 1)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        // One text tile with its own font, selected.
        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }

            public EditorHost()
            {
                var host = new Border();
                TransitionAssist.SetDisableTransitions(host, true);
                host.Resources.MergedDictionaries.Add(new CustomColorTheme
                {
                    BaseTheme = BaseTheme.Dark,
                    PrimaryColor = Color.FromRgb(2, 113, 249),
                    SecondaryColor = Colors.Lime
                });
                foreach (string resource in new[]
                {
                    "MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
                    "CapFrameX.View;component/Styles/CxTextFields.xaml"
                })
                {
                    host.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/" + resource)
                    });
                }
                Editor = new OsdEditorControl();
                var document = new OsdDesignDocument { Width = 640, CanvasHeight = 480, IsCanvas = true, GridSize = 4 };
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "text", Label = "Heading", Text = "Font fixture", Family = "Consolas",
                    X = 200, Y = 200, FrameWidth = 240, FrameHeight = 140
                });
                Editor.SetTemplateJson(document.ToJson());
                host.Child = Editor;
                Window = new Window
                {
                    Content = host, Width = 1280, Height = 960,
                    WindowStyle = WindowStyle.None, ShowActivated = false,
                    ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                Window.Show();
                Settle();
                Window.UpdateLayout();
                Editor.ResetHistory();
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
