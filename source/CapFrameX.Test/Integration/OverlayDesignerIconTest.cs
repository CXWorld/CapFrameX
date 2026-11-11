using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerIconTest
    {
        [TestMethod]
        public void AddedIconHasVisibleSizeControlThatResizesTheGlyphAndCanvasFrame()
        {
            using var host = new EditorHost();
            var tile = host.Editor.SelectedElement;
            Assert.AreEqual("icon", tile.Kind);
            Assert.AreEqual(28.0, tile.ValueSize);
            Assert.AreEqual(40.0, tile.FrameWidth.Value);
            Assert.IsTrue(host.Size.IsVisible, "Adding an icon must expose its own usable size control.");
            Assert.IsTrue(host.Size.IsEnabled);
            string before = host.Editor.GetTemplateJson();

            host.Size.SetCurrentValue(RangeBase.ValueProperty, 48.0);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(48.0, tile.ValueSize);
            Assert.AreEqual(40.0 * 48 / 28, tile.FrameWidth.Value, .001);
            Assert.AreEqual(40.0 * 48 / 28, tile.FrameHeight.Value, .001,
                "Increasing the glyph must also enlarge its canvas frame so it is not clipped.");
            Assert.AreEqual(48.0, NativeIcon(host.Editor)["size"].Value<double>(), .001);
            Assert.AreEqual(host.Editor.GetTemplateJson(), OsdDesignDocument.FromJson(host.Editor.GetTemplateJson()).ToJson());
            Undo(host);
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "One undo must restore the glyph and its entire frame.");
        }

        [TestMethod]
        public void IconSizeControlHonorsTheSupportedMaximumWithoutInvalidatingTheDesign()
        {
            using var host = new EditorHost();
            Assert.AreEqual(10.0, host.Size.Minimum);
            Assert.AreEqual(72.0, host.Size.Maximum);

            host.Size.SetCurrentValue(RangeBase.ValueProperty, 100.0);

            Assert.AreEqual(72.0, host.Size.Value);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(72.0, host.Editor.SelectedElement.ValueSize);
            Assert.AreEqual(40.0 * 72 / 28, host.Editor.SelectedElement.FrameWidth.Value, .001);
            Assert.AreEqual(72.0, NativeIcon(host.Editor)["size"].Value<double>(), .001);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ShrinkingAPaddedNarrowIconPreservesItsMinimumContentWidth(bool nested)
        {
            using var host = new EditorHost(nested: nested);
            var tile = host.Editor.SelectedElement;
            tile.Padding = 10;
            tile.FrameWidth = 28;
            tile.FrameHeight = 40;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();
            string before = host.Editor.GetTemplateJson();
            host.Editor.ResetHistory();

            host.Size.SetCurrentValue(RangeBase.ValueProperty, 10.0);

            Assert.IsTrue(host.Editor.TryCommitEdits(), "Shrinking a valid icon must retain room for its padding and minimum content width.");
            Assert.AreEqual(10.0, tile.ValueSize);
            Assert.AreEqual(28.0, tile.FrameWidth.Value, .001);
            Assert.AreEqual(8.0, tile.FrameWidth.Value - 2 * host.Editor.Document.EffectivePadding(tile), .001);
            Assert.AreEqual(10.0, NativeIcon(host.Editor)["size"].Value<double>(), .001);
            Undo(host);
            Assert.AreEqual(before, host.Editor.GetTemplateJson());
        }

        [TestMethod]
        public void EnlargingAnIconAtANestedCanvasEdgeKeepsItWithinItsParentAndUndoesItsPosition()
        {
            using var host = new EditorHost(nested: true);
            var tile = host.Editor.SelectedElement;
            var parent = host.Editor.CurrentContainer;
            Assert.IsNotNull(parent);
            var available = host.Editor.Document.CanvasSize(parent);
            Assert.AreEqual(80.0, available.Width);
            Assert.AreEqual(64.0, available.Height);
            tile.X = available.Width - tile.FrameWidth.Value;
            tile.Y = available.Height - tile.FrameHeight.Value;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();
            string before = host.Editor.GetTemplateJson();
            host.Editor.ResetHistory();

            host.Size.SetCurrentValue(RangeBase.ValueProperty, 72.0);

            Assert.IsTrue(host.Editor.TryCommitEdits(), "A size increase must fit the existing parent instead of invalidating the nested design.");
            Assert.AreEqual(64.0, tile.FrameWidth.Value, .001);
            Assert.AreEqual(64.0, tile.FrameHeight.Value, .001);
            Assert.AreEqual(44.8, tile.ValueSize, .001);
            Assert.AreEqual(44.8, host.Size.Value, .001, "The slider must show the size that actually fits inside its parent.");
            Assert.AreEqual(16.0, tile.X.Value, .001);
            Assert.AreEqual(0.0, tile.Y.Value, .001);
            Assert.IsTrue(tile.X.Value + tile.FrameWidth.Value <= available.Width);
            Assert.IsTrue(tile.Y.Value + tile.FrameHeight.Value <= available.Height);
            Assert.AreEqual(44.8, NativeIcon(host.Editor)["size"].Value<double>(), .001);
            Undo(host);
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "Undo must restore the original position as well as both dimensions and glyph size.");
            Assert.IsNotNull(host.Editor.CurrentContainer, "Undo must preserve the nested editing scope.");
        }

        [TestMethod]
        public void NetworkSelectionUsesTheIconFontSerializesThePresetAndUndoesAsOneEdit()
        {
            using var host = new EditorHost();
            // Imported profiles may use a different glyph font. Choosing a preset
            // must restore the preset's matching font together with its glyph.
            host.Editor.SelectedElement.Glyph = "A";
            host.Editor.SelectedElement.Family = "Consolas";
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();
            string before = host.Editor.GetTemplateJson();
            host.Editor.ResetHistory();
            var picker = host.Picker;
            object network = picker.Items.Cast<object>().Single(item =>
                (string)item.GetType().GetProperty("Glyph").GetValue(item) == "\uE968");
            picker.SetCurrentValue(Selector.SelectedItemProperty, network);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();
            host.Window.UpdateLayout();

            Assert.AreEqual("\uE968", host.Editor.SelectedElement.Glyph);
            Assert.AreEqual("Segoe MDL2 Assets", host.Editor.SelectedElement.Family);
            Assert.AreSame(network, picker.SelectedItem);
            Assert.IsTrue(picker.IsVisible);
            var displayedGlyph = VisualDescendants<TextBlock>(picker).Single(block => block.Text == "\uE968");
            Assert.AreEqual("Segoe MDL2 Assets", displayedGlyph.FontFamily.Source,
                "The picker must display its private-use glyph using the preset's icon font.");
            Assert.AreEqual("\uE968", NativeIcon(host.Editor)["glyph"].Value<string>());
            Assert.AreEqual("Segoe MDL2 Assets", NativeIcon(host.Editor)["family"].Value<string>());
            Undo(host);
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "One undo must restore both the previous glyph and its font.");
        }

        [TestMethod]
        [DataRow("WidthHandle", 0.5)]
        [DataRow("WidthHandle", 2.0)]
        [DataRow("HeightHandle", 0.5)]
        [DataRow("HeightHandle", 2.0)]
        public void CanvasResizeScalesTheGlyphAndBothAxesAndUndoesAsOneEdit(string handleName, double scale)
        {
            using var host = new EditorHost(scale);
            var tile = host.Editor.SelectedElement;
            string before = host.Editor.GetTemplateJson();
            var handle = (Thumb)host.Editor.FindName(handleName);
            InvokeEditor(host.Editor, "BeginResize", handle, new Point(0, 0));
            object gesture = ResizeGesture(host.Editor);
            Assert.IsNotNull(gesture, "An unlocked icon must support resizing with either canvas handle.");
            double gestureScale = (double)gesture.GetType().GetField("Scale").GetValue(gesture);
            Point pointer = handleName == "WidthHandle"
                ? new Point(40 * gestureScale, 0)
                : new Point(0, 40 * gestureScale);
            InvokeEditor(host.Editor, "UpdateResize", pointer);

            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "Pointer feedback must not mutate the saved design before release.");
            handle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Settle();

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(80.0, tile.FrameWidth.Value, .001);
            Assert.AreEqual(80.0, tile.FrameHeight.Value, .001, "Icon resizing must preserve its original frame aspect ratio.");
            Assert.AreEqual(56.0, tile.ValueSize, .001, "The glyph must visibly grow with its selection frame.");
            Assert.AreEqual(56.0, host.Size.Value, .001);
            Assert.AreEqual(56.0 * scale, NativeIcon(host.Editor)["size"].Value<double>(), .001);
            Undo(host);
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "One undo restores both frame dimensions and the glyph size.");
        }

        [TestMethod]
        public void LockedIconDisablesItsInputsAndRejectsCanvasResizing()
        {
            using var host = new EditorHost();
            host.Editor.SelectedElement.Locked = true;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();
            string before = host.Editor.GetTemplateJson();

            Assert.IsFalse(host.Size.IsEnabled);
            Assert.IsFalse(host.Picker.IsEnabled);
            var handle = (Thumb)host.Editor.FindName("WidthHandle");
            InvokeEditor(host.Editor, "BeginResize", handle, new Point(0, 0));
            Assert.IsNull(ResizeGesture(host.Editor));
            Assert.AreEqual(before, host.Editor.GetTemplateJson());
        }

        private static JObject NativeIcon(OsdEditorControl editor) =>
            JObject.Parse(editor.GetTemplateJson()).DescendantsAndSelf().OfType<JObject>()
                .Single(node => node["type"]?.Value<string>() == "icon");

        private static object ResizeGesture(OsdEditorControl editor) =>
            typeof(OsdEditorControl).GetField("_resize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);

        private static object InvokeEditor(OsdEditorControl editor, string method, params object[] arguments) =>
            typeof(OsdEditorControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, arguments);

        private static void Undo(EditorHost host)
        {
            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled);
        }

        private static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is T match) yield return match;
                foreach (T descendant in VisualDescendants<T>(child)) yield return descendant;
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

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }
            public Slider Size => (Slider)Editor.FindName("IconSizeSlider");
            public ComboBox Picker => (ComboBox)Editor.FindName("IconPicker");

            public EditorHost(double scale = 1, bool nested = false)
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
                var document = new OsdDesignDocument
                {
                    Width = 640, CanvasHeight = 480, IsCanvas = true, Scale = scale, GridSize = 4
                };
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "text", Label = "Heading", Text = "Icon editor fixture",
                    X = 200, Y = 200, FrameWidth = 240, FrameHeight = 100
                });
                if (nested)
                {
                    document.Tiles.Add(new OsdDesignTile
                    {
                        Kind = "container", Label = "Small icon container", Padding = 8,
                        X = 0, Y = 0, FrameWidth = 96, FrameHeight = 80
                    });
                }
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
                if (nested)
                {
                    Assert.IsTrue(Editor.EnterContainer(Editor.Document.Tiles.Single(tile => tile.Kind == "container")));
                }
                Editor.AddElement("icon");
                Assert.IsTrue(Editor.TryCommitEdits());
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
