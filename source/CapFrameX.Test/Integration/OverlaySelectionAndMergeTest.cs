using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    // Three requests for the tile editor: "Fit canvas to content" below both sizes of the canvas instead of
    // between them, several selected tiles (Ctrl+click) that one button places into a container of their
    // size, and adding the tiles of another design: one of its containers, or all of it.
    [STATestClass]
    [DoNotParallelize]
    public class OverlaySelectionAndMergeTest
    {
        [TestMethod]
        public void TheFitCanvasButtonSitsBelowTheHeightAndTheWidthOfTheCanvas()
        {
            using var host = new EditorHost(Board());
            var tabs = (TabControl)host.Editor.FindName("InspectorTabs");
            tabs.SelectedIndex = 1;
            Settle(50);
            host.Window.UpdateLayout();
            var height = (FrameworkElement)host.Editor.FindName("CanvasHeightField");
            var width = (FrameworkElement)host.Editor.FindName("DesignWidthField");
            var fit = (Button)host.Editor.FindName("FitCanvasButton");
            var fullWidth = (FrameworkElement)host.Editor.FindName("FullWidthCheckBox");
            double Top(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), tabs).Y;

            Assert.IsTrue(height.ActualHeight > 0 && width.ActualHeight > 0 && fit.ActualHeight > 0, "The Layout tab is not laid out.");
            Assert.IsTrue(Top(height) + height.ActualHeight <= Top(width), "The canvas height comes first.");
            Assert.IsTrue(Top(width) + width.ActualHeight <= Top(fit), "The button stands between the two sizes of the canvas.");
            Assert.IsTrue(Top(fit) + fit.ActualHeight <= Top(fullWidth), "The button belongs to the sizes above it.");
            StringAssert.Contains((string)fit.ToolTip, "the width to the rightmost tile", "The button says what it fits.");
        }

        [TestMethod]
        public void FitCanvasToContentFitsTheWidthAndTheHeightAsOneUndoableEdit()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            string before = editor.GetTemplateJson();
            var frames = editor.Document.Tiles.Select(editor.Document.TileFrame).ToArray();

            ((Button)editor.FindName("FitCanvasButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();

            Assert.IsTrue(editor.TryCommitEdits());
            Assert.AreEqual(472.0, editor.Document.Width, "The rightmost tile ends at 472.");
            Assert.AreEqual(160.0, editor.Document.CanvasHeight, "The lowest tile ends at 144; two grid cells follow.");
            CollectionAssert.AreEqual(frames, editor.Document.Tiles.Select(editor.Document.TileFrame).ToArray(), "The tiles stay where they are.");

            ((Button)editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();
            Assert.AreEqual(before, editor.GetTemplateJson(), "One undo restores both sizes.");
        }

        [TestMethod]
        // A tile that ends between two grid lines: the width takes the next line.
        [DataRow(600.0, 171.0, 336.0)]
        // Tiles up to the right edge of a canvas that is not on the grid: the width stays, it never grows.
        [DataRow(523.0, 363.0, 523.0)]
        // A design is at least 220 px wide.
        [DataRow(480.0, 0.0, 220.0)]
        // 472 plus the noise of adding two coordinates is 59 cells, not 60.
        [DataRow(480.0, 336.00000000000006, 472.0)]
        public void TheFittedWidthLandsOnTheGridAndNeverGrows(double width, double x, double expected)
        {
            var document = new OsdDesignDocument { Name = "Width", Width = width, CanvasHeight = 400, IsCanvas = true, Padding = 12, Gap = 8, GridSize = 8 };
            document.Tiles.Add(Metric("FPS", "fps", x, 0, x == 336.00000000000006 ? 136 : 160, 80));
            using var host = new EditorHost(document);

            ((Button)host.Editor.FindName("FitCanvasButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(expected, host.Editor.Document.Width);
            Assert.AreEqual(96.0, host.Editor.Document.CanvasHeight);
        }

        [TestMethod]
        public void AFlowDesignHasNoCanvasToFit()
        {
            using var host = new EditorHost(Flow());
            Assert.AreEqual(Visibility.Collapsed, ((Button)host.Editor.FindName("FitCanvasButton")).Visibility);
            Assert.AreEqual(Visibility.Visible, ((FrameworkElement)host.Editor.FindName("DesignWidthField")).Visibility);
        }

        [TestMethod]
        public void TilesPlacedIntoAContainerKeepTheirPlacesAndGiveItTheirSize()
        {
            var document = Board();
            var fps = document.Tiles[0];
            var low = document.Tiles[1];
            var gpu = document.Tiles[2];
            var heading = document.Tiles[3];
            var onScreen = new[] { fps, gpu }.Select(document.AbsoluteTileFrame).ToArray();

            var container = document.GroupIntoContainer(new[] { gpu, fps });

            Assert.AreEqual("container", container.Kind);
            Assert.AreEqual(new Rect(0, 0, 328, 144), document.TileFrame(container), "The container is the bounding box of its tiles.");
            Assert.AreEqual(0.0, container.Padding, "Padding would move the tiles or widen the frame.");
            Assert.AreEqual("#00000000", container.BackgroundColor, "A background would change the look.");
            CollectionAssert.AreEqual(new[] { fps, gpu }, container.Children.ToArray(), "The tiles keep their layer order.");
            CollectionAssert.AreEqual(onScreen, container.Children.Select(document.AbsoluteTileFrame).ToArray(), "The tiles moved on the screen.");
            CollectionAssert.AreEqual(new[] { low, container, heading }, document.Tiles.ToArray(), "The container takes the layer of its topmost tile.");
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void TheDesignLooksTheSameWithItsTilesInAContainer()
        {
            var document = Board();
            var before = Render(document);

            document.GroupIntoContainer(new[] { document.Tiles[0], document.Tiles[1], document.Tiles[2] });
            var after = Render(document);

            Assert.AreEqual(1f, before.Scale, "The preview scaled the design.");
            Assert.AreEqual(before.Scale, after.Scale);
            // The 480 x 400 canvas is centred in the picture; the three tiles cover (0, 0)..(328, 144) of it.
            AssertSamePixels(before.Pixels, after.Pixels, new Int32Rect(272, 312, 328, 144), new Point(272, 312), "tiles in the container");
            AssertSamePixels(before.Pixels, after.Pixels, new Int32Rect(0, 0, RenderSize, RenderSize), new Point(0, 0), "design", mostlyDrawn: false);
        }

        [TestMethod]
        public void ElementsOfAContainerCanBePlacedIntoAContainerOfTheirOwn()
        {
            var document = Board();
            var card = new OsdDesignTile { Kind = "container", Label = "Card", X = 8, Y = 160, FrameWidth = 400, FrameHeight = 200 };
            card.Children.Add(Metric("A", "fps", 16, 8, 96, 64));
            card.Children.Add(Metric("B", "fps", 120, 8, 96, 64));
            card.Children.Add(Metric("C", "fps", 16, 80, 96, 64));
            document.Tiles.Add(card);
            document.Validate();
            var onScreen = card.Children.Select(document.AbsoluteTileFrame).ToArray();
            var members = new[] { card.Children[0], card.Children[1] };
            var onScreenThird = card.Children[2];

            var inner = document.GroupIntoContainer(members);

            Assert.AreSame(card, document.GetParent(inner));
            Assert.AreEqual(new Rect(16, 8, 200, 64), document.TileFrame(inner));
            CollectionAssert.AreEqual(members, inner.Children.ToArray());
            CollectionAssert.AreEqual(new[] { inner, onScreenThird }, card.Children.ToArray(), "The container takes the layer of its topmost element.");
            CollectionAssert.AreEqual(onScreen, members.Append(onScreenThird).Select(document.AbsoluteTileFrame).ToArray(),
                "The elements moved on the screen.");
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void ARefusedPlacementChangesNothing()
        {
            var document = Board();
            var card = new OsdDesignTile { Kind = "container", Label = "Card", X = 8, Y = 160, FrameWidth = 400, FrameHeight = 100 };
            card.Children.Add(Metric("A", "fps", 16, 8, 96, 64));
            document.Tiles.Add(card);
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "classicRows", Label = "Rows", Key = null, ClassicRowsAutoHeight = true, Height = 120,
                X = 0, Y = 272, FrameWidth = 328, FrameHeight = 120
            });
            document.Validate();
            string before = document.ToJson();

            Assert.Throws<FormatException>(() => document.GroupIntoContainer(Array.Empty<OsdDesignTile>()), "Nothing was chosen.");
            Assert.Throws<FormatException>(() => document.GroupIntoContainer(new[] { document.Tiles[0], card.Children[0] }),
                "Tiles of two levels cannot share a container.");
            Assert.Throws<FormatException>(() => document.GroupIntoContainer(new[] { document.Tiles[2], document.Tiles[5] }),
                "A row block that follows its rows cannot sit in a container.");
            Assert.AreEqual(before, document.ToJson());

            // Four nested container levels are the limit: the innermost container cannot take another one.
            var deep = Board();
            var level = deep.Tiles[0];
            for (int depth = 0; depth < OsdDesignDocument.MaximumContainerDepth; depth++) level = deep.GroupIntoContainer(new[] { level });
            string nested = deep.ToJson();
            Assert.Throws<FormatException>(() => deep.GroupIntoContainer(new[] { level }));
            Assert.AreEqual(nested, deep.ToJson(), "A refused placement restores the tiles and their order.");

            Assert.Throws<FormatException>(() => Flow().GroupIntoContainer(new[] { Flow().Tiles[0] }), "Containers need a canvas.");
        }

        [TestMethod]
        public void CtrlClickAddsATileToTheSelectionAndTakesItOut()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            var tiles = editor.Document.Tiles.ToArray();
            var card = (FrameworkElement)editor.FindName("SelectionCard");
            var widthHandle = (FrameworkElement)editor.FindName("WidthHandle");
            Assert.IsTrue(editor.SelectElement(tiles[2]));
            // The outlines and handles follow the rectangles the native preview measured.
            for (int waited = 0; waited < 100 && widthHandle.Visibility != Visibility.Visible; waited++) Settle(30);
            CollectionAssert.AreEqual(new[] { tiles[2] }, editor.SelectedElements.ToArray());
            Assert.AreEqual(Visibility.Collapsed, card.Visibility);
            Assert.AreEqual(Visibility.Visible, widthHandle.Visibility, "One selected tile shows its resize handles.");

            // What the preview and the layer list do on Ctrl+click.
            InvokeEditor(editor, "SelectPressedTile", tiles[0], true);
            Settle();
            CollectionAssert.AreEqual(new[] { tiles[0], tiles[2] }, editor.SelectedElements.ToArray(), "The selection is in layer order.");
            Assert.AreSame(tiles[0], editor.SelectedElement, "The inspector follows the tile that was added.");
            Assert.AreEqual(Visibility.Visible, card.Visibility);
            Assert.AreEqual("2 tiles selected", ((TextBlock)editor.FindName("SelectionSummaryText")).Text);
            Assert.AreEqual(Visibility.Collapsed, widthHandle.Visibility, "The handles resize one tile.");
            Assert.AreEqual(1, AlsoSelectedOutlines(editor), "Every other selected tile shows an outline of its own.");

            Assert.IsTrue(editor.ToggleElementSelection(tiles[3]));
            Settle();
            CollectionAssert.AreEqual(new[] { tiles[0], tiles[2], tiles[3] }, editor.SelectedElements.ToArray());
            Assert.AreEqual(2, AlsoSelectedOutlines(editor));

            Assert.IsTrue(editor.ToggleElementSelection(tiles[3]));
            CollectionAssert.AreEqual(new[] { tiles[0], tiles[2] }, editor.SelectedElements.ToArray());
            Assert.AreSame(tiles[2], editor.SelectedElement, "Taking out the edited tile leaves the inspector on a selected one.");
            Assert.IsTrue(editor.ToggleElementSelection(tiles[0]));
            Assert.IsTrue(editor.ToggleElementSelection(tiles[2]));
            CollectionAssert.AreEqual(new[] { tiles[2] }, editor.SelectedElements.ToArray(), "The last selected tile stays selected.");

            Assert.IsTrue(editor.SelectElements(new[] { tiles[1], tiles[3] }));
            InvokeEditor(editor, "SelectPressedTile", tiles[1], false);
            Settle();
            CollectionAssert.AreEqual(new[] { tiles[1] }, editor.SelectedElements.ToArray(), "A plain click selects one tile.");
            Assert.AreEqual(Visibility.Collapsed, card.Visibility);
            Assert.AreEqual(0, AlsoSelectedOutlines(editor));
        }

        [TestMethod]
        public void TheLayerListMarksEverySelectedTile()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            ((TabControl)editor.FindName("PaletteTabs")).SelectedIndex = 1;
            Settle(50);
            host.Window.UpdateLayout();
            var list = (ListBox)editor.FindName("TileList");
            int Marked() => Descendants<Border>(list).Count(border => Equals(border.Tag, "selected") && border.Visibility == Visibility.Visible);

            Assert.IsTrue(editor.SelectElement(editor.Document.Tiles[0]));
            Settle();
            Assert.AreEqual(0, Marked(), "One selected tile is the row the list selects.");

            Assert.IsTrue(editor.SelectElements(new[] { editor.Document.Tiles[0], editor.Document.Tiles[2], editor.Document.Tiles[3] }));
            Settle();
            Assert.AreEqual(3, Marked());
            Assert.AreSame(editor.Document.Tiles[3], list.SelectedItem, "The list's own selection is the tile the inspector edits.");
        }

        [TestMethod]
        [DataRow("PlaceIntoContainerButton")]
        [DataRow("PlaceSelectionIntoContainerButton")]
        public void TheButtonPlacesTheSelectedTilesIntoAContainerAsOneUndoableEdit(string buttonName)
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            var button = (Button)editor.FindName(buttonName);
            string before = editor.GetTemplateJson();
            var tiles = editor.Document.Tiles.ToArray();
            Assert.IsTrue(editor.SelectElements(new[] { tiles[0], tiles[1], tiles[2] }));
            Settle();
            Assert.IsTrue(button.IsEnabled);

            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();

            Assert.IsTrue(editor.TryCommitEdits());
            Assert.AreEqual(2, editor.Document.Tiles.Count);
            var container = editor.Document.Tiles[0];
            Assert.AreEqual("container", container.Kind);
            Assert.AreEqual(new Rect(0, 0, 328, 144), editor.Document.TileFrame(container));
            CollectionAssert.AreEqual(new[] { "FPS", "1% LOW", "GPU" }, container.Children.Select(tile => tile.Label).ToArray());
            CollectionAssert.AreEqual(new[] { container }, editor.SelectedElements.ToArray(), "The new container is selected.");

            ((Button)editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();
            Assert.AreEqual(before, editor.GetTemplateJson(), "One undo takes the tiles out again.");
        }

        [TestMethod]
        public void ASingleTileCanBePlacedIntoAContainerAndLockedOnesCannot()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            var button = (Button)editor.FindName("PlaceIntoContainerButton");
            var tiles = editor.Document.Tiles.ToArray();
            tiles[1].Locked = true;
            Settle();
            Assert.IsTrue(editor.TryCommitEdits());
            string locked = editor.GetTemplateJson();

            Assert.IsTrue(editor.SelectElements(new[] { tiles[0], tiles[1] }));
            Settle();
            Assert.IsFalse(button.IsEnabled, "A locked tile stays where it is.");
            Assert.IsFalse(editor.PlaceSelectionIntoContainer());
            Assert.AreEqual(locked, editor.GetTemplateJson());

            Assert.IsTrue(editor.SelectElement(tiles[0]));
            Settle();
            Assert.IsTrue(button.IsEnabled);
            Assert.IsTrue(editor.PlaceSelectionIntoContainer());
            Assert.AreEqual("container", editor.Document.Tiles[0].Kind);
            Assert.AreEqual(new Rect(0, 0, 160, 80), editor.Document.TileFrame(editor.Document.Tiles[0]));
            Assert.AreSame(tiles[0], editor.Document.Tiles[0].Children.Single());
        }

        [TestMethod]
        public void AFlowDesignBecomesACanvasWhenItsTilesGoIntoAContainer()
        {
            using var host = new EditorHost(Flow());
            var editor = host.Editor;
            Assert.IsFalse(editor.Document.IsCanvas);
            var tiles = editor.Document.Tiles.ToArray();
            Assert.IsTrue(editor.SelectElements(new[] { tiles[0], tiles[1] }));

            Assert.IsTrue(editor.PlaceSelectionIntoContainer());

            Assert.IsTrue(editor.Document.IsCanvas, "Containers need a canvas, as when one is added from the palette.");
            Assert.AreEqual(2, editor.Document.Tiles.Count);
            var container = editor.Document.Tiles.Single(tile => tile.Kind == "container");
            CollectionAssert.AreEqual(new[] { tiles[0], tiles[1] }, container.Children.ToArray());
            var bounds = Rect.Empty;
            foreach (var child in container.Children) bounds.Union(editor.Document.AbsoluteTileFrame(child));
            var frame = editor.Document.TileFrame(container);
            Assert.AreEqual(bounds.X, frame.X, 1e-6);
            Assert.AreEqual(bounds.Y, frame.Y, 1e-6);
            Assert.AreEqual(bounds.Width, frame.Width, 1e-6);
            Assert.AreEqual(bounds.Height, frame.Height, 1e-6, "The container is the bounding box of the two metrics.");
            Assert.IsTrue(editor.TryCommitEdits());
        }

        [TestMethod]
        public void ARefusedPlacementKeepsTheSelection()
        {
            var document = Board();
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "classicRows", Label = "Rows", Key = null, ClassicRowsAutoHeight = true, Height = 120,
                X = 0, Y = 152, FrameWidth = 328, FrameHeight = 120
            });
            using var host = new EditorHost(document);
            var editor = host.Editor;
            string before = editor.GetTemplateJson();
            Assert.IsTrue(editor.SelectElements(new[] { editor.Document.Tiles[2], editor.Document.Tiles[4] }));
            Settle();
            Assert.IsFalse(((Button)editor.FindName("PlaceIntoContainerButton")).IsEnabled, "A row block that follows its rows cannot sit in a container.");

            Assert.IsFalse(editor.PlaceSelectionIntoContainer());

            Assert.AreEqual(before, editor.GetTemplateJson());
            CollectionAssert.AreEqual(new[] { "GPU", "Rows" }, editor.SelectedElements.Select(tile => tile.Label).ToArray(),
                "The tiles are selected again after the design was restored.");
        }

        [TestMethod]
        public void RemoveTakesEverySelectedTileAndKeepsOneInTheDesign()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            var tiles = editor.Document.Tiles.ToArray();
            Assert.IsTrue(editor.SelectElements(new[] { tiles[0], tiles[2] }));

            InvokeEditor(editor, "OnDelete", editor, new RoutedEventArgs());
            Settle();

            CollectionAssert.AreEqual(new[] { tiles[1], tiles[3] }, editor.Document.Tiles.ToArray());
            Assert.AreEqual(1, editor.SelectedElements.Count);

            Assert.IsTrue(editor.SelectElements(editor.Document.Tiles.ToArray()));
            InvokeEditor(editor, "OnDelete", editor, new RoutedEventArgs());
            Settle();
            Assert.AreEqual(2, editor.Document.Tiles.Count, "A design keeps at least one tile.");
        }

        [TestMethod]
        public void EscapeLeavesTheTileTheInspectorEdits()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            var tiles = editor.Document.Tiles.ToArray();
            Assert.IsTrue(editor.SelectElements(new[] { tiles[0], tiles[1], tiles[3] }));
            Assert.AreSame(tiles[3], editor.SelectedElement);

            var preview = (UIElement)editor.FindName("PreviewSurface");
            preview.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(preview), 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });

            CollectionAssert.AreEqual(new[] { tiles[3] }, editor.SelectedElements.ToArray());
        }

        [TestMethod]
        public void MergedTilesGoBelowTheOthersInTheirArrangement()
        {
            var target = Board();
            target.CanvasHeight = 160;
            var source = Other();
            string original = source.ToJson();
            var own = target.Tiles.ToArray();
            var ownFrames = own.Select(target.TileFrame).ToArray();

            var added = target.Merge(source);

            Assert.AreEqual(original, source.ToJson(), "The design the tiles come from stays as it is.");
            CollectionAssert.AreEqual(own.Concat(added).ToArray(), target.Tiles.ToArray(), "The added tiles are the topmost layers.");
            CollectionAssert.AreEqual(ownFrames, own.Select(target.TileFrame).ToArray());
            // The lowest tile ends at 144; the gap of 8 puts the block at 152. It starts at the left edge.
            for (int index = 0; index < added.Count; index++)
            {
                var from = source.TileFrame(source.Tiles[index]);
                Assert.AreEqual(new Rect(from.X - 16, 152 + from.Y - 24, from.Width, from.Height), target.TileFrame(added[index]), source.Tiles[index].Label);
                Assert.AreNotSame(source.Tiles[index], added[index]);
            }
            var card = added.Single(tile => tile.Kind == "container");
            CollectionAssert.AreEqual(source.Tiles.Single(tile => tile.Kind == "container").Children.Select(source.TileFrame).ToArray(),
                card.Children.Select(target.TileFrame).ToArray(), "A container keeps its contents.");
            Assert.AreEqual(520.0, target.Width, "The canvas grows to the width of the added tiles.");
            Assert.AreEqual(392.0, target.CanvasHeight, "The canvas grows to hold them.");
            AssertRoundTrip(target);
        }

        [TestMethod]
        public void OnlyTheChosenTilesAreMerged()
        {
            var target = Board();
            var source = Other();
            var card = source.Tiles.Single(tile => tile.Kind == "container");

            var added = target.Merge(source, new[] { card });

            Assert.AreEqual(1, added.Count);
            Assert.AreEqual(5, target.Tiles.Count);
            Assert.AreEqual(new Rect(0, 152, 240, 120), target.TileFrame(added[0]), "A single container starts at the left edge below the others.");
            CollectionAssert.AreEqual(card.Children.Select(tile => tile.Label).ToArray(), added[0].Children.Select(tile => tile.Label).ToArray());
            Assert.AreEqual(480.0, target.Width);
            AssertRoundTrip(target);

            Assert.Throws<FormatException>(() => target.Merge(source, Array.Empty<OsdDesignTile>()), "Nothing was chosen.");
            Assert.Throws<FormatException>(() => target.Merge(source, new[] { card.Children[0] }), "Only top-level tiles are taken.");
        }

        [TestMethod]
        public void MergedTilesKeepTheLookTheirDesignGaveThem()
        {
            var target = Board();
            var source = Other();
            var targetLook = Look(target, target.Tiles);

            var added = target.Merge(source);

            // Accent, text color, tile color and padding differ: what a tile left to them is now its own.
            var pairs = Pairs(source.Tiles, added).ToArray();
            Assert.AreEqual(source.EnumerateTiles().Count(), pairs.Length);
            foreach (var (from, to) in pairs)
            {
                foreach (var part in OsdTileStyle.PartsOf(from))
                    Assert.AreEqual(OsdTileStyle.Resolve(source, from, part.Field), OsdTileStyle.Resolve(target, to, part.Field), true,
                        $"{from.Label}: {part.Field}");
                Assert.AreEqual(source.EffectivePadding(from), target.EffectivePadding(to), from.Label + ": padding");
            }
            var chart = added.Single(tile => tile.Kind == "chart");
            Assert.AreEqual(source.Accent, chart.ChartLines[0].Color, "A value line without a color drew the accent.");
            Assert.IsNull(chart.ChartLines[1].Color, "A stutter threshold draws the same color in every design.");
            CollectionAssert.AreEqual(targetLook, Look(target, target.Tiles.Take(4)), "The design's own tiles keep their look.");
            Assert.AreEqual("#0271F9FF", target.Accent, "The design keeps its defaults.");
            Assert.AreEqual(12.0, target.Padding);
        }

        [TestMethod]
        public void TilesOfADesignWithTheSameDefaultsAreTakenAsTheyAre()
        {
            var target = Board();
            var source = Other();
            source.Accent = target.Accent; source.TextColor = target.TextColor; source.TileColor = target.TileColor; source.Padding = target.Padding;

            var added = target.Merge(source);

            foreach (var (from, to) in Pairs(source.Tiles, added))
            {
                foreach (string field in OsdTileStyle.Fields)
                    Assert.AreEqual(OsdTileStyle.GetColor(from, field), OsdTileStyle.GetColor(to, field), $"{from.Label}: {field}");
                Assert.AreEqual(from.Padding, to.Padding, from.Label);
            }
            Assert.IsNull(added.Single(tile => tile.Kind == "chart").ChartLines[0].Color);
        }

        [TestMethod]
        public void TheRendererDrawsMergedTilesAsItDrewThemInTheirDesign()
        {
            // Same canvas, scale, font and corner radius; accent, colors and padding differ.
            var target = Board();
            target.CanvasHeight = 640;
            var source = Other();
            // Rearranged for a canvas 480 px wide: the container below the metric, the chart below that, all
            // from the left edge. The tiles then move down by 128 px only, a whole number of the preview's
            // checkerboard periods (two 16 px cells), so the translucent tiles lie on the same backdrop.
            source.Tiles[0].X = 0; source.Tiles[1].X = 128;
            source.Tiles[2].X = 0; source.Tiles[2].Y = 104;
            source.Tiles[3].X = 0; source.Tiles[3].Y = 232; source.Tiles[3].FrameWidth = 448;
            source.Width = 480; source.CanvasHeight = 640;
            source.Validate();
            var drawn = Render(source);
            // The same tiles copied as they are take the other design's defaults: the comparison sees that.
            var copied = Board();
            copied.CanvasHeight = 640;
            foreach (var tile in source.Tiles)
            {
                var copy = tile.Copy();
                copy.Y += 128;
                copied.Tiles.Add(copy);
            }
            var plain = Render(copied);
            var temp = drawn.Tiles[0];
            Assert.IsTrue(Differences(drawn.Pixels, plain.Pixels, new Int32Rect((int)temp.X, (int)temp.Y, (int)temp.Width, (int)temp.Height),
                new Point(plain.Tiles[4].X, plain.Tiles[4].Y)).Different > 1000, "A tile in other colors and padding would pass as the same.");

            var added = target.Merge(source);
            var merged = Render(target);

            Assert.AreEqual(1f, drawn.Scale, "The preview scaled the design.");
            Assert.AreEqual(drawn.Scale, merged.Scale);
            Assert.AreEqual(640.0, target.CanvasHeight);
            Assert.AreEqual(128.0, added[0].Y.Value - source.Tiles[0].Y.Value);
            Assert.AreEqual(0.0, added[0].X.Value - source.Tiles[0].X.Value);
            for (int index = 0; index < added.Count; index++)
            {
                var from = drawn.Tiles[index];
                var to = merged.Tiles[4 + index];
                Assert.AreEqual(from.Width, to.Width, .01, source.Tiles[index].Label);
                Assert.AreEqual(from.Height, to.Height, .01, source.Tiles[index].Label);
                AssertSamePixels(drawn.Pixels, merged.Pixels,
                    new Int32Rect((int)Math.Round(from.X), (int)Math.Round(from.Y), (int)Math.Round(from.Width), (int)Math.Round(from.Height)),
                    new Point(Math.Round(to.X), Math.Round(to.Y)), source.Tiles[index].Label);
            }
        }

        [TestMethod]
        public void ARowBlockThatFollowsItsRowsMovesBelowTheMergedTiles()
        {
            var target = Board();
            var rows = new OsdDesignTile
            {
                Kind = "classicRows", Label = "Rows", Key = null, ClassicRowsAutoHeight = true, Height = 120,
                X = 0, Y = 152, FrameWidth = 328, FrameHeight = 120
            };
            target.Tiles.Add(rows);
            target.Validate();

            var added = target.Merge(Other());

            // The block has to stay the lowest tile of its columns: the added tiles take its place.
            Assert.AreEqual(152.0, added.Min(tile => tile.Y.Value));
            double bottom = added.Max(tile => target.TileFrame(tile).Bottom);
            Assert.IsTrue(rows.Y.Value >= bottom, $"The row block at {rows.Y} would cover the tiles above {bottom}.");
            Assert.AreEqual(0.0, rows.Y.Value % target.GridSize);
            Assert.IsTrue(target.CanvasHeight >= rows.Y.Value + rows.FrameHeight.Value);
            AssertRoundTrip(target);
        }

        [TestMethod]
        public void AFlowDesignIsLaidOutBeforeItIsMerged()
        {
            var target = Board();
            var source = Flow();
            string original = source.ToJson();

            var added = target.Merge(source);

            Assert.AreEqual(original, source.ToJson(), "The flow design stays one.");
            Assert.AreEqual(3, added.Count);
            Assert.IsTrue(added.All(tile => tile.X.HasValue && tile.Y.HasValue && tile.FrameWidth > 0 && tile.FrameHeight > 0));
            Assert.AreEqual(152.0, added.Min(tile => tile.Y.Value));
            Assert.AreEqual(added[0].Y, added[1].Y, "Two tiles of one span share a row.");
            Assert.IsTrue(added[1].X > added[0].X);
            Assert.IsTrue(added[2].Y > added[0].Y, "A full-width tile takes the next row.");
            AssertRoundTrip(target);

            // The other way round, the flow design becomes a canvas, and tiles of four columns fit its two.
            var flow = Flow();
            var wide = Other();
            wide.Columns = 4;
            wide.Tiles[0].ColumnSpan = 4;
            var taken = flow.Merge(wide);
            Assert.IsTrue(flow.IsCanvas);
            Assert.AreEqual(2, taken[0].ColumnSpan);
            Assert.AreEqual(3 + wide.Tiles.Count, flow.Tiles.Count);
            AssertRoundTrip(flow);
        }

        [TestMethod]
        public void ARefusedMergeChangesNothing()
        {
            var target = Board();
            var crowd = Board();
            for (int index = 0; index < 59; index++) crowd.Tiles.Add(Metric("M" + index, "fps", 0, 0, 96, 48));
            crowd.Validate();
            string before = target.ToJson();

            Assert.Throws<FormatException>(() => target.Merge(crowd), "A design holds 64 tiles.");
            Assert.AreEqual(before, target.ToJson());

            var tall = Board();
            tall.CanvasHeight = 4096;
            tall.Tiles[2].Y = 4000;
            tall.Validate();
            Assert.Throws<FormatException>(() => target.Merge(tall, new[] { tall.Tiles[0], tall.Tiles[2] }), "A canvas is at most 4096 px tall.");
            Assert.AreEqual(before, target.ToJson());

            Assert.Throws<ArgumentException>(() => target.Merge(target));
            Assert.Throws<ArgumentNullException>(() => target.Merge(null));
        }

        [TestMethod]
        public void MergingThroughTheEditorIsOneUndoableEditAndSelectsTheAddedTiles()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            var source = Other();
            string before = editor.GetTemplateJson();
            int edits = 0;
            editor.DocumentEdited += (_, _) => edits++;

            int added = editor.MergeDesign(source.ToJson());
            Settle();

            Assert.AreEqual(4, added);
            Assert.IsTrue(edits > 0, "The host has to learn of the edit.");
            Assert.AreEqual(8, editor.Document.Tiles.Count);
            CollectionAssert.AreEqual(editor.Document.Tiles.Skip(4).ToArray(), editor.SelectedElements.ToArray(),
                "The added tiles are selected, ready to be placed into a container.");
            Assert.IsTrue(((Button)editor.FindName("PlaceSelectionIntoContainerButton")).IsEnabled);
            Assert.IsTrue(editor.TryCommitEdits());

            ((Button)editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();
            Assert.AreEqual(before, editor.GetTemplateJson(), "One undo removes the added tiles.");
        }

        [TestMethod]
        public void TheEditorMergesTheChosenTilesIntoTheDesignWhateverContainerIsOpen()
        {
            var document = Board();
            var card = new OsdDesignTile { Kind = "container", Label = "Card", X = 8, Y = 160, FrameWidth = 400, FrameHeight = 100 };
            card.Children.Add(Metric("A", "fps", 16, 8, 96, 64));
            document.Tiles.Add(card);
            using var host = new EditorHost(document);
            var editor = host.Editor;
            Assert.IsTrue(editor.EnterContainer(editor.Document.Tiles[4]));
            var source = Other();
            int container = source.Tiles.IndexOf(source.Tiles.Single(tile => tile.Kind == "container"));

            Assert.AreEqual(1, editor.MergeDesign(source.ToJson(), new[] { container }));

            Assert.IsNull(editor.CurrentContainer, "The tiles join the design's own tiles.");
            Assert.AreEqual(6, editor.Document.Tiles.Count);
            Assert.AreEqual("Readings", editor.Document.Tiles[5].Label);
            Assert.AreEqual(1, editor.Document.Tiles[4].Children.Count);
            CollectionAssert.AreEqual(new[] { editor.Document.Tiles[5] }, editor.SelectedElements.ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => editor.MergeDesign(source.ToJson(), new[] { 9 }));
        }

        [TestMethod]
        public void TheEditorBindsMergedTilesToThisPcAndSaysWhyItRefusesADesign()
        {
            using var host = new EditorHost(Board());
            var editor = host.Editor;
            editor.Sources = new[]
            {
                new OsdTelemetrySource { Key = "sensor:gpu0/temperature/2", SemanticKey = "gpuTemp", Name = "GPU Core", Category = "Graphics", Unit = "°C" },
                new OsdTelemetrySource { Key = "fps", SemanticKey = "fps", Name = "Framerate", Category = "Performance", Unit = "FPS" }
            };
            // A starter nobody opened yet still names its readings by role.
            var starter = new OsdDesignDocument { Name = "Starter", Width = 320, CanvasHeight = 160, IsCanvas = true, GridSize = 8 };
            starter.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "GPU TEMP", Key = "gpuTemp", Unit = "", X = 0, Y = 0, FrameWidth = 160, FrameHeight = 80 });

            Assert.AreEqual(1, editor.MergeDesign(starter.ToJson()));
            Assert.AreEqual("sensor:gpu0/temperature/2", editor.Document.Tiles[4].Key);
            Assert.AreEqual("°C", editor.Document.Tiles[4].Unit);

            var crowd = Board();
            for (int index = 0; index < 59; index++) crowd.Tiles.Add(Metric("M" + index, "fps", 0, 0, 96, 48));
            string before = editor.GetTemplateJson();
            var refused = Assert.Throws<FormatException>(() => editor.MergeDesign(crowd.ToJson()));
            StringAssert.Contains(refused.Message, "64 tiles");
            Assert.AreEqual(before, editor.GetTemplateJson(), "A refused merge leaves the design as it was.");
            Assert.Throws<FormatException>(() => editor.MergeDesign("{ \"version\": 1 }"), "Not a design of this editor.");
            Assert.AreEqual(before, editor.GetTemplateJson());
        }

        private const int RenderSize = 1024;

        // A 480 x 400 canvas: two metrics side by side, a bar below them, a heading at the right.
        private static OsdDesignDocument Board()
        {
            var document = new OsdDesignDocument { Name = "Board", Width = 480, CanvasHeight = 400, IsCanvas = true, Padding = 12, Gap = 8, GridSize = 8 };
            document.Tiles.Add(Metric("FPS", "fps", 0, 0));
            document.Tiles.Add(Metric("1% LOW", "fps1pctLow", 168, 0));
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "bar", Key = "gpuLoad", Label = "GPU", Unit = "%", Minimum = 0, Maximum = 100, X = 0, Y = 88, FrameWidth = 328, FrameHeight = 56
            });
            document.Tiles.Add(new OsdDesignTile { Kind = "text", Label = "Heading", Text = "BOARD", X = 336, Y = 0, FrameWidth = 136, FrameHeight = 48 });
            document.Validate();
            return document;
        }

        // Another design with its own defaults (red accent, light text, dark red tiles, padding 6). Its
        // tiles start at (16, 24): a metric, a bar, a container with two elements and a chart with a
        // stutter threshold, 520 px wide.
        private static OsdDesignDocument Other()
        {
            var document = new OsdDesignDocument
            {
                Name = "Other", Width = 560, CanvasHeight = 320, IsCanvas = true, Padding = 6, Gap = 8, GridSize = 8,
                Accent = "#FF3030FF", TextColor = "#FFE0E0FF", TileColor = "#301010EB"
            };
            document.Tiles.Add(Metric("TEMP", "gpuTemp", 16, 24, 120, 72));
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "bar", Key = "cpuLoad", Label = "CPU", Unit = "%", Minimum = 0, Maximum = 100, X = 144, Y = 24, FrameWidth = 144, FrameHeight = 48
            });
            var card = new OsdDesignTile { Kind = "container", Label = "Readings", X = 296, Y = 24, FrameWidth = 240, FrameHeight = 120 };
            card.Children.Add(new OsdDesignTile { Kind = "metric", Label = "POWER", Key = "gpuPower", Unit = "W", X = 0, Y = 0, FrameWidth = 104, FrameHeight = 72 });
            card.Children.Add(new OsdDesignTile
            {
                Kind = "text", Label = "Note", Text = "PMD", Padding = 0, BackgroundColor = "#00000000", X = 112, Y = 8, FrameWidth = 96, FrameHeight = 32
            });
            document.Tiles.Add(card);
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "chart", Label = "FRAME TIME", Key = "frametimes", Unit = "ms", X = 16, Y = 152, FrameWidth = 520, FrameHeight = 112,
                ChartLines = new List<OsdChartLine>
                {
                    new OsdChartLine { Series = "frametimes", Kind = "value" },
                    new OsdChartLine { Series = "frametimes", Kind = "stutter" }
                }
            });
            document.Validate();
            return document;
        }

        // A two-column flow design: two metrics in a row and a bar across both columns.
        private static OsdDesignDocument Flow()
        {
            var document = new OsdDesignDocument { Name = "Flow", Width = 320, Columns = 2, Padding = 12, Gap = 8 };
            document.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS" });
            document.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "1% LOW", Key = "fps1pctLow", Unit = "FPS" });
            document.Tiles.Add(new OsdDesignTile { Kind = "bar", Label = "GPU", Key = "gpuLoad", Unit = "%", Minimum = 0, Maximum = 100, ColumnSpan = 2 });
            document.Validate();
            return document;
        }

        private static OsdDesignTile Metric(string label, string key, double x, double y, double width = 160, double height = 80) => new OsdDesignTile
        {
            Kind = "metric", Label = label, Key = key, Unit = "FPS", X = x, Y = y, FrameWidth = width, FrameHeight = height
        };

        // Every tile of one design beside its copy in the other, containers' elements included.
        private static IEnumerable<(OsdDesignTile From, OsdDesignTile To)> Pairs(IEnumerable<OsdDesignTile> from, IEnumerable<OsdDesignTile> to)
        {
            foreach (var (source, copy) in from.Zip(to))
            {
                yield return (source, copy);
                foreach (var pair in Pairs(source.Children, copy.Children)) yield return pair;
            }
        }

        // The colors the renderer draws for the tiles, and their padding.
        private static string[] Look(OsdDesignDocument document, IEnumerable<OsdDesignTile> tiles) => tiles.SelectMany(tile =>
            OsdTileStyle.PartsOf(tile).Select(part => tile.Label + " " + part.Field + " " + OsdTileStyle.Resolve(document, tile, part.Field))
                .Append(tile.Label + " padding " + document.EffectivePadding(tile))).ToArray();

        private static (byte[] Pixels, float Scale, OsdPreviewTileRect[] Tiles) Render(OsdDesignDocument document)
        {
            const int stride = RenderSize * 4;
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(RenderSize, RenderSize), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(document.ToJson()), "The renderer rejected the design.");
            Assert.IsTrue(preview.SetMetricsJson(JsonSerializer.Serialize(new Dictionary<string, double>
            {
                ["fps"] = 143.2, ["fps1pctLow"] = 97.4, ["gpuLoad"] = 71, ["cpuLoad"] = 38, ["gpuTemp"] = 64, ["gpuPower"] = 281
            })));
            var pixels = new byte[stride * RenderSize];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, 0.05));
            }
            finally
            {
                handle.Free();
            }
            var tiles = new OsdPreviewTileRect[OsdDesignDocument.MaximumTiles];
            int count = preview.GetLayout(out var layout, tiles);
            Assert.AreEqual(document.Tiles.Count, count);
            return (pixels, layout.Scale, tiles.Take(count).ToArray());
        }

        // The pixels of `area` in one picture against those at `at` in the other. A clip that cuts a rounded
        // corner may antialias single pixels a few steps differently (OSD CLAUDE.md, "Exactness").
        private static void AssertSamePixels(byte[] expected, byte[] actual, Int32Rect area, Point at, string what, bool mostlyDrawn = true)
        {
            var (different, worst, drawn, where, first) = Differences(expected, actual, area, at);
            Assert.IsTrue(drawn > (mostlyDrawn ? area.Width * area.Height / 2 : 0), $"The {what} was not drawn ({drawn} of {area.Width * area.Height} pixels).");
            Assert.IsTrue(worst <= 6 && different <= 64,
                $"The {what} is drawn differently: {different} values differ, by up to {worst}, within {where}; first at {first} (BGRA).");
        }

        private static (int Different, int Worst, int Drawn, Rect Where, string First) Differences(byte[] expected, byte[] actual, Int32Rect area, Point at)
        {
            int different = 0, worst = 0, drawn = 0;
            var where = Rect.Empty;
            string first = null;
            for (int y = 0; y < area.Height; y++)
                for (int x = 0; x < area.Width; x++)
                {
                    int from = ((area.Y + y) * RenderSize + area.X + x) * 4, to = (((int)at.Y + y) * RenderSize + (int)at.X + x) * 4;
                    // Anything but the two colors of the preview's checkerboard (BGRA 30 2A 2A, 2A 21 21).
                    if (!(expected[from + 1] == 0x2A && expected[from + 2] == 0x2A && expected[from] == 0x30)
                        && !(expected[from + 1] == 0x21 && expected[from + 2] == 0x21 && expected[from] == 0x2A)) drawn++;
                    for (int channel = 0; channel < 4; channel++)
                    {
                        int difference = Math.Abs(expected[from + channel] - actual[to + channel]);
                        if (difference == 0) continue;
                        different++;
                        worst = Math.Max(worst, difference);
                        where.Union(new Point(x, y));
                        first ??= $"({x}, {y}): {expected[from]:X2}{expected[from + 1]:X2}{expected[from + 2]:X2}{expected[from + 3]:X2} against "
                            + $"{actual[to]:X2}{actual[to + 1]:X2}{actual[to + 2]:X2}{actual[to + 3]:X2}";
                    }
                }
            return (different, worst, drawn, where, first);
        }

        private static int AlsoSelectedOutlines(OsdEditorControl editor) =>
            ((Canvas)editor.FindName("InteractionCanvas")).Children.OfType<Rectangle>().Count(outline => outline.Visibility == Visibility.Visible);

        private static void AssertRoundTrip(OsdDesignDocument document)
        {
            string json = document.ToJson();
            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson());
        }

        private static object InvokeEditor(OsdEditorControl editor, string method, params object[] arguments) =>
            typeof(OsdEditorControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, arguments);

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
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

            public void Dispose()
            {
                Window.Close();
                Editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Window.Content = null;
            }
        }
    }
}
