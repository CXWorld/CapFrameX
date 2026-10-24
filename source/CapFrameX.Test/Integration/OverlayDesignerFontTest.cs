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
    /// <summary>The overlay editor offers one font for the whole design, on its Style tab.</summary>
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerFontTest
    {
        [TestMethod]
        public void TextTilesDrawTheDesignFontAndOnlyAnIconNamesItsOwn()
        {
            using var host = new EditorHost();
            var tile = host.Editor.SelectedElement;
            var typography = (Expander)host.Editor.FindName("TypographyExpander");
            Assert.IsNull(tile.Family);
            Assert.IsFalse(LogicalDescendants<ComboBox>(host.Editor).Any(combo =>
                BindingOperations.GetBindingExpression(combo, ComboBox.TextProperty)?.ParentBinding.Path.Path == "SelectedTile.Family"),
                "A tile offers no font of its own.");

            foreach (var kind in new[] { "metric", "bar", "gauge", "chart", "classicRows", "text" })
            {
                tile.Kind = kind;
                Settle();
                Assert.IsNull(tile.Family, $"A {kind} tile draws the design font.");
                Assert.AreEqual(kind is "metric" or "text" ? Visibility.Visible : Visibility.Collapsed, typography.Visibility,
                    $"Only metrics and texts align and size their text; a {kind} tile has nothing to set there.");
            }

            tile.Kind = "icon";
            Settle();
            tile.Family = "Segoe Fluent Icons";
            Settle();
            Assert.AreEqual(Visibility.Collapsed, typography.Visibility, "An icon takes its font from the icon picker.");
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual("Segoe Fluent Icons", JObject.Parse(host.Editor.GetTemplateJson())["root"]["children"][0]["children"][0]["family"].Value<string>(),
                "An icon keeps its glyph font.");

            tile.Kind = "text";
            Settle();
            Assert.IsNull(tile.Family, "An icon font draws no text.");
        }

        [TestMethod]
        public void StyleTabFontBecomesTheDesignThemeAndUndoRestoresIt()
        {
            using var host = new EditorHost();
            var styleTab = (TabItem)host.Editor.FindName("StyleTab");
            var picker = LogicalDescendants<ComboBox>(styleTab).SingleOrDefault(combo =>
                BindingOperations.GetBindingExpression(combo, ComboBox.TextProperty)?.ParentBinding.Path.Path == "Document.FontFamily");
            Assert.IsNotNull(picker, "The style tab must offer the design font.");
            Assert.AreEqual(1, LogicalDescendants<ComboBox>(host.Editor).Count(combo =>
                BindingOperations.GetBindingExpression(combo, ComboBox.TextProperty)?.ParentBinding.Path.Path == "Document.FontFamily"),
                "The design font has one place, the style tab.");
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

        // One text tile, selected.
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
                    Kind = "text", Label = "Heading", Text = "Font fixture",
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
