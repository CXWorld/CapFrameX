using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayGroupFitHeightTest
    {
        private const double ContentFrameHeight = 125.2;

        [TestMethod]
        [DataRow("metric", false, true, true)]
        [DataRow("horizontal", false, true, true)]
        [DataRow("vertical", false, true, true)]
        [DataRow("horizontal", true, true, false)]
        [DataRow("vertical", true, true, false)]
        [DataRow("metric", false, false, false)]
        [DataRow("vertical", false, false, false)]
        public void FitHeightIsOfferedOnlyForManuallySizedCanvasGroups(string display, bool automatic, bool canvas, bool visible)
        {
            var document = CreateDocument();
            document.IsCanvas = canvas;
            var group = document.Tiles[0];
            group.GroupDisplay = display == "metric" ? "metric" : "bar";
            group.GroupBarOrientation = display == "vertical" ? "vertical" : null;
            if (automatic)
            {
                group.GroupBarWidth = 100;
                group.GroupBarHeight = 20;
            }
            if (!canvas)
            {
                group.X = group.Y = group.FrameWidth = group.FrameHeight = null;
            }
            using var host = new EditorHost(document);

            Assert.AreEqual("Fit height to content", host.FitButton.Content);
            Assert.AreEqual(visible, host.FitButton.IsVisible);
        }

        [TestMethod]
        public void ActivatingAutomaticBarDimensionsHidesFitAndUndoRestoresIt()
        {
            using var host = new EditorHost(CreateDocument());
            Assert.IsTrue(host.FitButton.IsVisible);
            var width = (TextBox)host.Editor.FindName("GroupBarWidthField");
            width.SetCurrentValue(TextBox.TextProperty, "100");

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.IsTrue(host.Editor.SelectedElement.HasExplicitGroupBarLayout);
            Assert.IsFalse(host.FitButton.IsVisible, "Visibility must react to dimensions edited on the currently selected group.");
            host.Undo();
            Assert.IsFalse(host.Editor.SelectedElement.HasExplicitGroupBarLayout);
            Assert.IsTrue(host.FitButton.IsVisible);
        }

        [TestMethod]
        [DataRow(8.0)]
        [DataRow(16.0)]
        [DataRow(48.0)]
        public void FitUsesExactContentHeightWithoutInventingAnOverlapFromGridRounding(double gridSize)
        {
            var document = CreateDocument(80);
            document.GridSize = gridSize;
            document.Tiles.Add(Neighbor(ContentFrameHeight + 1));
            using var host = new EditorHost(document);
            string before = host.Editor.GetTemplateJson();
            double neighborY = host.Editor.Document.Tiles[1].Y.Value;
            int changes = 0;
            host.Editor.TemplateChanged += (_, _) => changes++;

            host.Fit();

            var group = host.Editor.SelectedElement;
            Assert.AreEqual(ContentFrameHeight, group.FrameHeight.Value, .001,
                "Fitting must not round the content height up to the next placement-grid cell.");
            Assert.AreEqual(344.0, group.FrameWidth.Value);
            Assert.AreEqual(0.0, group.X.Value);
            Assert.AreEqual(0.0, group.Y.Value);
            Assert.AreEqual(37.5, group.Height);
            Assert.AreEqual(2, group.GroupColumns);
            Assert.AreEqual(neighborY, host.Editor.Document.Tiles[1].Y.Value, "A manual fit must retain neighboring positions.");
            Assert.AreEqual("Tile height fitted to content.", host.Status);
            Assert.AreEqual(1, changes);

            host.Undo();

            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "A single undo must restore the complete fit operation.");
            Assert.IsFalse(host.UndoButton.IsEnabled);
        }

        [TestMethod]
        public void RepeatingFitReportsNoChangeWithoutAnUndoEntryOrAnotherPreviewUpdate()
        {
            using var host = new EditorHost(CreateDocument(256));
            host.Fit();
            Assert.AreEqual("Tile height fitted to content.", host.Status);
            Assert.AreEqual(ContentFrameHeight, host.Editor.SelectedElement.FrameHeight.Value, .001);
            host.Editor.ResetHistory();
            string before = host.Editor.GetTemplateJson();
            int changes = 0;
            host.Editor.TemplateChanged += (_, _) => changes++;

            host.Fit();

            Assert.AreEqual(before, host.Editor.GetTemplateJson());
            Assert.AreEqual("Tile height already fits the content.", host.Status,
                "An unchanged fit must replace an earlier success message with neutral feedback.");
            Assert.IsFalse(host.UndoButton.IsEnabled);
            Assert.AreEqual(0, changes);
        }

        [TestMethod]
        public void GenuineOverlapIsRejectedAndLeavesTheDocumentUnchanged()
        {
            var document = CreateDocument(80);
            document.Tiles.Add(Neighbor(ContentFrameHeight - 1));
            using var host = new EditorHost(document);
            string before = host.Editor.GetTemplateJson();
            int changes = 0;
            host.Editor.TemplateChanged += (_, _) => changes++;

            host.Fit();

            Assert.AreEqual(before, host.Editor.GetTemplateJson());
            StringAssert.Contains(host.Status, "overlap");
            Assert.IsFalse(host.UndoButton.IsEnabled);
            Assert.AreEqual(0, changes, "Rejected geometry must not publish an overlay update.");
        }

        [TestMethod]
        public void FittingAnAlreadyExactGroupDoesNotRoundTheCanvasHeight()
        {
            var document = CreateDocument(ContentFrameHeight);
            document.CanvasHeight = 130;
            string before = document.ToJson();

            document.FitGroupToContent(document.Tiles[0]);

            Assert.AreEqual(before, document.ToJson());
            Assert.AreEqual(130.0, document.CanvasHeight);
        }

        [TestMethod]
        public void NestedFitGrowsItsContainerOnlyByTheExactRequiredHeight()
        {
            var document = CreateDocument(80);
            var group = document.Tiles[0];
            document.Tiles.Clear();
            document.Width = 420;
            var container = new OsdDesignTile
            {
                Kind = "container", Label = "CPU panel", X = 0, Y = 0,
                FrameWidth = 400, FrameHeight = 112, Padding = 12
            };
            container.Children.Add(group);
            document.Tiles.Add(container);
            var neighbor = Neighbor(ContentFrameHeight + 24 + 1);
            document.Tiles.Add(neighbor);
            double originalNeighborY = neighbor.Y.Value;
            double parentGrowth = ContentFrameHeight + 24 - container.FrameHeight.Value;

            document.FitGroupToContent(group);

            Assert.AreEqual(ContentFrameHeight, group.FrameHeight.Value, .001);
            Assert.AreEqual(ContentFrameHeight + 24, container.FrameHeight.Value, .001);
            Assert.AreEqual(originalNeighborY + parentGrowth, neighbor.Y.Value, .001,
                "Container growth may move following tiles, but must not add grid-rounding space to that movement.");
            Assert.IsTrue(document.TileFrame(container).Bottom < document.TileFrame(neighbor).Top);
            Assert.IsNotNull(document.ToJson(), "Exact nested bounds must remain serializable.");
            group.FrameHeight += .01;
            Assert.ThrowsExactly<FormatException>(() => document.ToJson(),
                "Tolerating floating-point noise must not allow a child to escape its container.");
        }

        private static OsdDesignDocument CreateDocument(double frameHeight = 256)
        {
            var document = new OsdDesignDocument
            {
                Width = 344, Columns = 1, Padding = 12, GridSize = 16,
                CanvasHeight = 600, IsCanvas = true
            };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "group", Label = "CPU threads", GroupKey = "cpu.loads", GroupDisplay = "bar", GroupColumns = 2,
                GroupBarOrientation = "vertical", GroupShowLabels = false, GroupShowValues = false,
                Unit = "%", Minimum = 0, Maximum = 100, Height = 37.5,
                X = 0, Y = 0, FrameWidth = 344, FrameHeight = frameHeight,
                GroupMembers = Enumerable.Range(1, 4).Select(index => new OsdGroupMember
                {
                    Key = "cpu.load." + index, Label = "Thread " + index, IsEnabled = true
                }).ToList()
            });
            return document;
        }

        private static OsdDesignTile Neighbor(double y) => new OsdDesignTile
        {
            Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS",
            X = 0, Y = y, FrameWidth = 344, FrameHeight = 64
        };

        private static void Settle()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(1) };
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

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }
            public Button FitButton => (Button)Editor.FindName("FitGroupHeightButton");
            public Button UndoButton => (Button)Editor.FindName("UndoButton");
            public string Status => LogicalDescendants<TextBlock>(Editor).Single(block =>
                block.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == "Status").Text;

            public EditorHost(OsdDesignDocument document)
            {
                Editor = new OsdEditorControl();
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
                Editor.ResetHistory();
            }

            public void Fit()
            {
                Assert.IsTrue(FitButton.IsVisible);
                FitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Settle();
            }

            public void Undo()
            {
                Assert.IsTrue(UndoButton.IsEnabled);
                UndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
