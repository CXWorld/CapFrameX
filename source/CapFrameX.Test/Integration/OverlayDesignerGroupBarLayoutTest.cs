using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerGroupBarLayoutTest
    {
        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void BarWidthCommitsOnEnterWrapsAndUndoesAsOneEdit(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.Commit("GroupBarWidthField", "64");
            var tile = host.Editor.SelectedElement;
            Assert.AreEqual(2, host.Layout.Rows);
            double trackHeight = host.Layout.TrackHeight;
            double frameHeight = tile.FrameHeight.Value;
            string before = host.Editor.GetTemplateJson();
            host.Editor.ResetHistory();
            var field = host.Field("GroupBarWidthField");

            field.SetCurrentValue(TextBox.TextProperty, "128");
            Settle(350);

            Assert.AreEqual(64.0, tile.GroupBarWidth);
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "Typing must not repack the group before Enter or focus loss.");
            Assert.IsTrue(host.Editor.HasPendingTextEdits);
            Enter(field);
            Settle();

            Assert.AreEqual(128.0, tile.GroupBarWidth);
            Assert.AreEqual(4, host.Layout.Rows, "Wide bars must move to additional rows.");
            Assert.AreEqual(4, host.Rows.SelectedItem);
            Assert.AreEqual(trackHeight, host.Layout.TrackHeight, .001);
            Assert.IsTrue(tile.FrameHeight > frameHeight, "Wrapping must enlarge the tile rather than clip the new rows.");
            Assert.IsFalse(host.Editor.HasPendingTextEdits);
            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "One undo restores all synchronized geometry.");
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled);
            Assert.AreEqual(2, host.Rows.SelectedItem);
        }

        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void RowsChoiceUpdatesWidthAndShowsOnlyAchievablePacking(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.Commit("GroupBarWidthField", "64");
            host.Rows.SetCurrentValue(ComboBox.SelectedItemProperty, 4);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(4, host.Layout.Rows);
            Assert.AreEqual(2, host.Layout.Columns);
            Assert.IsTrue(host.Editor.SelectedElement.GroupBarWidth > 100);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 8 }, host.Rows.Items.Cast<int>().ToArray());
            Assert.AreEqual(host.Editor.SelectedElement.GroupBarWidth.Value,
                double.Parse(host.Field("GroupBarWidthField").Text), .001);
        }

        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void TileWidthFitsEveryBarWithoutChangingRowsAndTileHeightResizesEveryTrack(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.Commit("GroupBarWidthField", "64");
            host.Commit("GroupBarHeightField", "20");
            host.CommitBinding("SelectedFrameWidth", "168");

            Assert.AreEqual(2, host.Layout.Rows);
            Assert.AreEqual(4, host.Layout.Columns);
            AssertFillsWidth(host);
            Assert.AreEqual(20.0, host.Layout.TrackHeight, .001, "A horizontal tile resize must retain the track height.");
            Assert.AreEqual(2, host.Rows.SelectedItem);
            double height = host.Editor.SelectedElement.FrameHeight.Value;

            // The pointer resize path must use the same model operation as numeric fields.
            typeof(OsdEditorControl).GetMethod("ResizeElement", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(host.Editor, new object[] { host.Editor.SelectedElement, 168.0, height + 40 });
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(40.0, host.Layout.TrackHeight, .001);
            Assert.AreEqual(2, host.Layout.Rows);
            Assert.AreEqual(40.0, double.Parse(host.Field("GroupBarHeightField").Text), .001);
            Assert.AreEqual(height + 40, host.Editor.SelectedElement.FrameHeight.Value, .001);
        }

        [DataTestMethod]
        [DataRow("horizontal", 0.5, false)]
        [DataRow("horizontal", 2.0, false)]
        [DataRow("horizontal", 0.5, true)]
        [DataRow("horizontal", 2.0, true)]
        [DataRow("vertical", 0.5, false)]
        [DataRow("vertical", 2.0, false)]
        [DataRow("vertical", 0.5, true)]
        [DataRow("vertical", 2.0, true)]
        public void WidthHandleFitsBarsToStretchedTilePreservesRowsAndUndoesAsOneEdit(string orientation, double scale, bool nested)
        {
            using var host = new EditorHost(orientation, documentWidth: 640, scale: scale, nested: nested);
            host.Commit("GroupBarWidthField", "64");
            host.Commit("GroupBarHeightField", "20");
            var tile = host.Editor.SelectedElement;
            double height = tile.FrameHeight.Value;
            string before = host.Editor.GetTemplateJson();
            host.Editor.ResetHistory();
            var handle = (Thumb)host.Editor.FindName("WidthHandle");
            InvokeEditor(host.Editor, "BeginResize", handle, new Point(0, 0));
            var gesture = typeof(OsdEditorControl).GetField("_resize", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(host.Editor);
            Assert.IsNotNull(gesture, "The selected group's border must support a width gesture.");
            double gestureScale = (double)gesture.GetType().GetField("Scale").GetValue(gesture);
            InvokeEditor(host.Editor, "UpdateResize", new Point((560 - 344) * gestureScale, 0));

            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "Pointer feedback must not mutate the design before release.");
            handle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Settle();

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(560.0, tile.FrameWidth.Value, .001);
            Assert.AreEqual(4, host.Layout.Columns);
            Assert.AreEqual(2, host.Layout.Rows);
            Assert.AreEqual(2, host.Rows.SelectedItem);
            Assert.AreEqual(20.0, host.Layout.TrackHeight, .001);
            Assert.AreEqual(height, tile.FrameHeight.Value, .001, "A width drag must preserve track height and the group's height.");
            Assert.IsTrue(tile.GroupBarWidth > 64, "Stretching the tile must widen bars rather than leave trailing empty space.");
            AssertFillsWidth(host);
            Assert.AreEqual(tile.GroupBarWidth.Value, double.Parse(host.Field("GroupBarWidthField").Text), .001);
            Assert.AreEqual(host.Editor.GetTemplateJson(), OsdDesignDocument.FromJson(host.Editor.GetTemplateJson()).ToJson());

            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "One undo must restore the tile and all bar dimensions.");
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled);
        }

        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void LegacyGroupWidthResizePreservesExistingPackingAndActivatesSynchronizedDimensions(string orientation)
        {
            using var host = new EditorHost(orientation, documentWidth: 640);
            var previous = host.Layout;
            Assert.IsNull(host.Editor.SelectedElement.GroupBarWidth);
            Assert.IsNull(host.Editor.SelectedElement.GroupBarHeight);

            host.CommitBinding("SelectedFrameWidth", "560");

            Assert.AreEqual(previous.Columns, host.Layout.Columns);
            Assert.AreEqual(previous.Rows, host.Layout.Rows);
            Assert.AreEqual(previous.TrackHeight, host.Layout.TrackHeight, .001);
            Assert.IsNotNull(host.Editor.SelectedElement.GroupBarWidth);
            Assert.IsNotNull(host.Editor.SelectedElement.GroupBarHeight);
            AssertFillsWidth(host);
            Assert.AreEqual(host.Layout.Rows, host.Rows.SelectedItem);
        }

        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void CombinedResizeFitsWidthAndHeightAgainstThePreservedRows(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.Commit("GroupBarWidthField", "64");
            host.Commit("GroupBarHeightField", "20");
            double newHeight = host.Editor.SelectedElement.FrameHeight.Value + 40;

            InvokeEditor(host.Editor, "ResizeElement", host.Editor.SelectedElement, 168.0, newHeight);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(4, host.Layout.Columns);
            Assert.AreEqual(2, host.Layout.Rows);
            Assert.AreEqual(40.0, host.Layout.TrackHeight, .001);
            Assert.AreEqual(newHeight, host.Editor.SelectedElement.FrameHeight.Value, .001);
            AssertFillsWidth(host);
        }

        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void ExtremelyNarrowTileReducesColumnsOnlyWhenOnePixelBarsNoLongerFit(string orientation)
        {
            var document = new OsdDesignDocument { Width = 344, Padding = 12, CanvasHeight = 800, IsCanvas = true };
            var tile = new OsdDesignTile
            {
                Kind = "group", Label = "CPU threads", GroupKey = "cpu.loads", GroupDisplay = "bar", GroupColumns = 32,
                GroupBarOrientation = orientation == "vertical" ? "vertical" : null,
                GroupShowLabels = false, GroupShowValues = false, Unit = "%", Minimum = 0, Maximum = 100,
                X = 0, Y = 0, FrameWidth = 344, FrameHeight = 256,
                GroupMembers = Enumerable.Range(1, 32).Select(index => new OsdGroupMember
                {
                    Key = "cpu.load." + index, Label = "Thread " + index, IsEnabled = true
                }).ToList()
            };
            document.Tiles.Add(tile);
            document.SetGroupBarWidth(tile, 1);
            document.SetGroupBarHeight(tile, 20);
            double gap = document.GetGroupBarLayout(tile).Gap;
            double padding = document.EffectivePadding(tile);
            double minimumInnerWidthFor32Columns = 32 + 31 * gap;

            document.ResizeGroupBars(tile, minimumInnerWidthFor32Columns + 2 * padding, tile.FrameHeight.Value);

            Assert.AreEqual(32, document.GetGroupBarLayout(tile).Columns, "Exact one-pixel bars must preserve the existing columns.");
            Assert.AreEqual(1.0, tile.GroupBarWidth.Value, .001);
            Assert.AreEqual(1, document.GetGroupBarLayout(tile).Rows);
            double oneRowHeight = tile.FrameHeight.Value;

            document.ResizeGroupBars(tile, minimumInnerWidthFor32Columns - .25 + 2 * padding, tile.FrameHeight.Value);

            Assert.AreEqual(31, document.GetGroupBarLayout(tile).Columns, "Only the column that no longer fits may be removed.");
            Assert.AreEqual(2, document.GetGroupBarLayout(tile).Rows);
            Assert.IsTrue(tile.FrameHeight > oneRowHeight, "The extra row must enlarge the tile instead of clipping its members.");
            Assert.IsTrue(tile.GroupBarWidth >= 1);

            const double narrowInnerWidth = 64;
            document.ResizeGroupBars(tile, narrowInnerWidth + 2 * padding, tile.FrameHeight.Value);

            var layout = document.GetGroupBarLayout(tile);
            int fittingColumns = Math.Max(1, (int)Math.Floor((narrowInnerWidth + gap) / (1 + gap)));
            Assert.AreEqual(fittingColumns, layout.Columns);
            Assert.AreEqual((int)Math.Ceiling(32.0 / fittingColumns), layout.Rows);
            Assert.AreEqual(20.0, layout.TrackHeight, .001, "Width-only narrowing must keep the track height, even when rows increase.");
            Assert.AreEqual(narrowInnerWidth, layout.EffectiveWidth * layout.Columns + gap * (layout.Columns - 1), .001);
            Assert.AreEqual(layout.ContentHeight + 2 * padding, tile.FrameHeight.Value, .001);
            Assert.AreEqual(document.ToJson(), OsdDesignDocument.FromJson(document.ToJson()).ToJson(), "The narrowest supported tile must remain valid and serializable.");
        }

        [DataTestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void HeightOnlyResizeRetainsManuallyChosenBarWidthAndTrailingSpace(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.Commit("GroupBarWidthField", "64");
            host.Commit("GroupBarHeightField", "20");
            double width = host.Editor.SelectedElement.FrameWidth.Value;
            double height = host.Editor.SelectedElement.FrameHeight.Value;
            double occupiedWidth = host.Layout.Columns * host.Layout.EffectiveWidth + (host.Layout.Columns - 1) * host.Layout.Gap;
            Assert.IsTrue(occupiedWidth < width - 2 * host.Editor.Document.EffectivePadding(host.Editor.SelectedElement));

            host.CommitBinding("SelectedFrameHeight", (height + 40).ToString());

            Assert.AreEqual(width, host.Editor.SelectedElement.FrameWidth.Value, .001);
            Assert.AreEqual(64.0, host.Editor.SelectedElement.GroupBarWidth.Value, .001);
            Assert.AreEqual(64.0, double.Parse(host.Field("GroupBarWidthField").Text), .001);
            Assert.AreEqual(4, host.Layout.Columns);
            Assert.AreEqual(2, host.Layout.Rows);
            Assert.AreEqual(40.0, host.Layout.TrackHeight, .001);
            Assert.AreEqual(occupiedWidth, host.Layout.Columns * host.Layout.EffectiveWidth + (host.Layout.Columns - 1) * host.Layout.Gap, .001);
        }

        [TestMethod]
        public void MemberAndDirectionChangesKeepRowsAndFrameHeightSynchronized()
        {
            using var host = new EditorHost("horizontal");
            host.Commit("GroupBarWidthField", "100");
            host.Commit("GroupBarHeightField", "20");
            var tile = host.Editor.SelectedElement;
            Assert.AreEqual(3, host.Layout.Rows);
            double originalHeight = tile.FrameHeight.Value;

            tile.GroupMembers[6].IsEnabled = false;
            tile.GroupMembers[7].IsEnabled = false;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(2, host.Layout.Rows);
            Assert.AreEqual(2, host.Rows.SelectedItem);
            Assert.IsTrue(tile.FrameHeight < originalHeight);
            ((ComboBox)host.Editor.FindName("GroupBarDirection")).SetCurrentValue(ComboBox.SelectedItemProperty, "Vertical");
            tile.GroupShowLabels = true;
            tile.GroupShowValues = true;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual("vertical", tile.GroupBarOrientation);
            Assert.AreEqual(20.0, host.Layout.TrackHeight, .001);
            Assert.AreEqual(host.Layout.ContentHeight + 2 * host.Editor.Document.EffectivePadding(tile), tile.FrameHeight.Value, .001);
            Assert.AreEqual(host.Layout.Rows, host.Rows.SelectedItem);
        }

        [TestMethod]
        public void FlowGroupAlwaysOffersDimensionsWithoutConvertingTheDesign()
        {
            using var host = new EditorHost("horizontal", canvas: false);
            Assert.IsTrue(host.Field("GroupBarWidthField").IsVisible);
            Assert.IsTrue(host.Field("GroupBarHeightField").IsVisible);
            Assert.IsTrue(host.Rows.IsVisible);
            host.Commit("GroupBarWidthField", "128");
            host.Commit("GroupBarHeightField", "16");

            Assert.IsFalse(host.Editor.Document.IsCanvas);
            Assert.IsNull(host.Editor.SelectedElement.FrameWidth);
            Assert.AreEqual(4, host.Layout.Rows);
            Assert.AreEqual(16.0, host.Layout.TrackHeight);
            Assert.AreEqual(4, host.Rows.SelectedItem);
        }

        [TestMethod]
        public void OpeningAndChangingLegacyDirectionDoNotActivateExplicitDimensions()
        {
            using var host = new EditorHost("horizontal");
            var tile = host.Editor.SelectedElement;
            Assert.IsNull(tile.GroupBarWidth);
            Assert.IsNull(tile.GroupBarHeight);
            ((ComboBox)host.Editor.FindName("GroupBarDirection")).SetCurrentValue(ComboBox.SelectedItemProperty, "Vertical");
            ((CheckBox)host.Editor.FindName("GroupBarValues")).SetCurrentValue(CheckBox.IsCheckedProperty, false);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();
            Assert.IsNull(tile.GroupBarWidth, "Legacy templates retain their existing packing until a dimension or row count is edited.");
            Assert.IsNull(tile.GroupBarHeight);
        }

        [TestMethod]
        public void InvalidBarDimensionsRetainTheLastValidLayout()
        {
            using var host = new EditorHost("vertical");
            host.Commit("GroupBarWidthField", "64");
            string before = host.Editor.GetTemplateJson();
            var field = host.Field("GroupBarHeightField");
            field.SetCurrentValue(TextBox.TextProperty, "-1");
            Enter(field);
            Assert.IsTrue(Validation.GetHasError(field));
            Assert.IsFalse(host.Editor.TryCommitEdits());
            Assert.AreEqual(before, host.Editor.Document.ToJson());
            host.Commit("GroupBarHeightField", "24");
            Assert.IsFalse(Validation.GetHasError(field));
            Assert.AreEqual(24.0, host.Layout.TrackHeight);
        }

        [TestMethod]
        public void TemporarilyEmptySelectionCanRecoverWithoutLosingTheGroup()
        {
            using var host = new EditorHost("vertical");
            host.Commit("GroupBarWidthField", "64");
            var tile = host.Editor.SelectedElement;
            foreach (var member in tile.GroupMembers) member.IsEnabled = false;
            Settle(350);
            Assert.IsFalse(host.Editor.TryCommitEdits(), "An empty group must remain invalid without an unhandled layout exception.");
            Assert.IsTrue(host.Rows.Items.Count > 0);

            tile.GroupMembers[0].IsEnabled = true;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(1, host.Layout.Rows);
            Assert.AreEqual(1, host.Rows.SelectedItem);
        }

        [TestMethod]
        public void CaptionOverflowRetainsPreviewRejectsSaveAndCanRecoverOrUndo()
        {
            using var host = new EditorHost("horizontal", memberCount: 128);
            host.Commit("GroupBarWidthField", "320");
            host.Commit("GroupBarHeightField", "12");
            string valid = host.Editor.GetTemplateJson();
            int previews = 0;
            host.Editor.TemplateChanged += (_, _) => previews++;
            var values = (CheckBox)host.Editor.FindName("GroupBarValues");

            values.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            Settle(350);

            Assert.IsTrue(host.Editor.HasValidationErrors);
            Assert.IsFalse(host.Editor.TryCommitEdits());
            Assert.AreEqual(0, previews, "Overflow must retain the last accepted preview.");
            values.SetCurrentValue(CheckBox.IsCheckedProperty, false);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(valid, host.Editor.GetTemplateJson());

            values.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            Assert.IsFalse(host.Editor.TryCommitEdits());
            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsFalse(host.Editor.HasValidationErrors);
            Assert.IsTrue(host.Editor.TryCommitEdits());
        }

        private static void Enter(TextBox field)
        {
            var args = new KeyEventArgs(new TestKeyboardDevice(), PresentationSource.FromVisual(field), Environment.TickCount, Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            field.RaiseEvent(args);
            Assert.IsTrue(args.Handled);
        }

        private static void AssertFillsWidth(EditorHost host)
        {
            var layout = host.Layout;
            double innerWidth = host.Editor.SelectedElement.FrameWidth.Value -
                2 * host.Editor.Document.EffectivePadding(host.Editor.SelectedElement);
            Assert.AreEqual(innerWidth, layout.EffectiveWidth * layout.Columns + layout.Gap * (layout.Columns - 1),
                .001, "The last bar in a full row must reach the tile's inner right edge.");
        }

        private static object InvokeEditor(OsdEditorControl editor, string method, params object[] arguments) =>
            typeof(OsdEditorControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, arguments);

        private static void Settle(int milliseconds = 1)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (object item in LogicalTreeHelper.GetChildren(parent))
            {
                if (item is not DependencyObject child) continue;
                if (child is T match) yield return match;
                foreach (T descendant in LogicalDescendants<T>(child)) yield return descendant;
            }
        }

        private sealed class TestKeyboardDevice : KeyboardDevice
        {
            public TestKeyboardDevice() : base(InputManager.Current) { }
            protected override KeyStates GetKeyStatesFromSystem(Key key) => KeyStates.None;
        }

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }
            public GroupBarLayout Layout => Editor.Document.GetGroupBarLayout(Editor.SelectedElement);
            public ComboBox Rows => (ComboBox)Editor.FindName("GroupBarRows");

            public EditorHost(string orientation, bool canvas = true, int memberCount = 8,
                double documentWidth = 344, double scale = 1, bool nested = false)
            {
                Editor = new OsdEditorControl();
                var document = new OsdDesignDocument { Width = documentWidth, Columns = 1, Padding = 12, CanvasHeight = 800, IsCanvas = canvas, Scale = scale };
                var tile = new OsdDesignTile
                {
                    Kind = "group", Label = "CPU threads", GroupKey = "cpu.loads", GroupDisplay = "bar", GroupColumns = 4,
                    GroupBarOrientation = orientation == "vertical" ? "vertical" : null,
                    GroupShowLabels = false, GroupShowValues = false, Unit = "%", Minimum = 0, Maximum = 100,
                    X = canvas ? 0 : null, Y = canvas ? 0 : null,
                    FrameWidth = canvas ? 344 : null, FrameHeight = canvas ? 256 : null,
                    GroupMembers = Enumerable.Range(1, memberCount).Select(index => new OsdGroupMember
                    {
                        Key = "cpu.load." + index, Label = "Thread " + index, IsEnabled = true
                    }).ToList()
                };
                if (nested)
                {
                    var container = new OsdDesignTile
                    {
                        Kind = "container", Label = "Hardware", Padding = 16,
                        X = 0, Y = 0, FrameWidth = documentWidth, FrameHeight = 600
                    };
                    container.Children.Add(tile);
                    document.Tiles.Add(container);
                }
                else document.Tiles.Add(tile);
                Editor.SetTemplateJson(document.ToJson());
                Window = new Window
                {
                    Content = Editor, Width = 1280, Height = 960,
                    WindowStyle = WindowStyle.None, ShowActivated = false,
                    ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                Window.Show();
                Settle();
                Window.UpdateLayout();
                if (nested)
                {
                    Assert.IsTrue(Editor.EnterContainer(Editor.Document.Tiles[0]));
                    Assert.IsTrue(Editor.SelectElement(Editor.CurrentElements[0]));
                    Settle();
                }
                Editor.ResetHistory();
            }

            public TextBox Field(string name) => (TextBox)Editor.FindName(name);

            public void Commit(string name, string value)
            {
                var field = Field(name);
                field.SetCurrentValue(TextBox.TextProperty, value);
                Enter(field);
                Assert.IsFalse(Validation.GetHasError(field));
                Assert.IsTrue(Editor.TryCommitEdits());
                Settle();
            }

            public void CommitBinding(string path, string value)
            {
                var field = LogicalDescendants<TextBox>((TabControl)Editor.FindName("InspectorTabs")).Single(box =>
                    box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path == path);
                field.SetCurrentValue(TextBox.TextProperty, value);
                Enter(field);
                Assert.IsTrue(Editor.TryCommitEdits());
                Settle();
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
