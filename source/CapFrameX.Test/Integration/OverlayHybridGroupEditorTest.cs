using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayHybridGroupEditorTest
    {
        [TestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void CoreTypeToggleSeparatesRowsFitsHeightAndUndoesAsOneEdit(string orientation)
        {
            using var host = new EditorHost(orientation);
            Assert.IsTrue(host.CoreTypes.IsVisible);
            Assert.AreEqual(2, host.Layout.Rows);
            double oldHeight = host.Editor.SelectedElement.FrameHeight.Value;
            string before = host.Editor.GetTemplateJson();
            host.Editor.ResetHistory();

            host.SeparateTypes(true);

            Assert.IsTrue(host.Editor.SelectedElement.GroupByCoreType == true);
            CollectionAssert.AreEqual(new[] { 6, 8, 2 }, host.Layout.RowCounts.ToArray());
            Assert.AreEqual(3, host.Rows.SelectedItem);
            Assert.IsTrue(host.Editor.SelectedElement.FrameHeight > oldHeight);
            Assert.AreEqual(host.Layout.ContentHeight + 24, host.Editor.SelectedElement.FrameHeight.Value, .001);
            Assert.AreEqual(host.Editor.GetTemplateJson(), OsdDesignDocument.FromJson(host.Editor.GetTemplateJson()).ToJson());

            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();

            Assert.AreEqual(before, host.Editor.GetTemplateJson());
            Assert.IsFalse(host.CoreTypes.IsChecked == true);
            Assert.AreEqual(2, host.Rows.SelectedItem);
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled);
        }

        [TestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void RowsChoiceAccountsForCoreTypeBoundaries(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.SeparateTypes(true);

            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 8, 16 }, host.Rows.Items.Cast<int>().ToArray());
            host.Rows.SetCurrentValue(ComboBox.SelectedItemProperty, 5);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(5, host.Layout.Rows);
            Assert.AreEqual(4, host.Layout.Columns);
            CollectionAssert.AreEqual(new[] { 4, 2, 4, 4, 2 }, host.Layout.RowCounts.ToArray());
            Assert.AreEqual(5, host.Rows.SelectedItem);
            Assert.IsTrue(host.Editor.SelectedElement.GroupByCoreType == true);
            Assert.AreEqual(host.Layout.ContentHeight + 24, host.Editor.SelectedElement.FrameHeight.Value, .001);
        }

        [TestMethod]
        [DataRow("horizontal")]
        [DataRow("vertical")]
        public void DisablingACompleteCoreTypeRemovesItsRowAndKeepsChoicesSynchronized(string orientation)
        {
            using var host = new EditorHost(orientation);
            host.SeparateTypes(true);
            double previousHeight = host.Editor.SelectedElement.FrameHeight.Value;

            foreach (var member in host.Editor.SelectedElement.GroupMembers.Where(member => member.CoreType == "LPE"))
                member.IsEnabled = false;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            CollectionAssert.AreEqual(new[] { 6, 8 }, host.Layout.RowCounts.ToArray());
            Assert.AreEqual(2, host.Rows.SelectedItem);
            Assert.IsTrue(host.Rows.Items.Cast<int>().Contains(2));
            Assert.IsTrue(host.Editor.SelectedElement.FrameHeight < previousHeight);
            Assert.AreEqual(host.Layout.ContentHeight + 24, host.Editor.SelectedElement.FrameHeight.Value, .001);
            Assert.AreEqual(2, host.Editor.SelectedElement.GroupMembers.Count(member => !member.IsEnabled));
        }

        [TestMethod]
        public void SourcesWithoutTopologyKeepLegacyLabelsAndHideTheToggle()
        {
            using var host = new EditorHost("vertical", topology: false);
            Assert.IsFalse(host.CoreTypes.IsVisible);
            Assert.AreEqual(2, host.Layout.Rows);
            Assert.IsFalse(host.Editor.GetTemplateJson().Contains("\"coreType\""));
            var group = OsdSourceGroup.Build(new[]
            {
                Source(1, null), Source(2, null)
            }).Single();

            var members = group.CreateTile(1).GroupMembers;

            CollectionAssert.AreEqual(new[] { "C1 T1", "C2 T1" }, members.Select(member => member.Label).ToArray());
            Assert.IsTrue(members.All(member => member.CoreType == null));
        }

        [TestMethod]
        public void SourceGroupCarriesTopologyAndLabelsIntoIndependentMemberCopies()
        {
            var group = OsdSourceGroup.Build(new[] { Source(1, "P"), Source(9, "E"), Source(17, "LPE") }).Single();
            var members = group.CreateTile(1).GroupMembers;

            CollectionAssert.AreEqual(new[] { "P C1 T1", "E C9 T1", "LP-E C17 T1" }, members.Select(member => member.Label).ToArray());
            var copy = members[0].Copy();
            Assert.AreEqual("P", copy.CoreType);
            Assert.AreEqual(1, copy.CoreIndex);
            Assert.AreEqual(1, copy.ThreadIndex);
            copy.CoreType = "E";
            copy.CoreIndex = 20;
            copy.ThreadIndex = 2;
            Assert.AreEqual("P", members[0].CoreType);
            Assert.AreEqual(1, members[0].CoreIndex);
            Assert.AreEqual(1, members[0].ThreadIndex);
        }

        [TestMethod]
        public void NewlyInsertedHybridSourceGroupUsesPhysicalCoreBoundaries()
        {
            var sources = Enumerable.Range(1, 24).Select(index => Source(index, index <= 8 ? "P" : "E"));
            var group = OsdSourceGroup.Build(sources).Single();

            var tile = group.CreateTile(1);
            var document = new OsdDesignDocument { Width = 600, Columns = 1, Padding = 12 };
            document.Tiles.Add(tile);

            Assert.IsTrue(tile.GroupByCoreType == true);
            Assert.AreEqual(8, tile.GroupColumns);
            // Vertical tracks can fit all eight requested columns on this tile.
            tile.GroupBarOrientation = "vertical";
            tile.GroupShowLabels = false;
            tile.GroupShowValues = false;
            CollectionAssert.AreEqual(new[] { 8, 8, 8 }, document.GetGroupBarLayout(tile).RowCounts.ToArray());
        }

        [TestMethod]
        [DataRow(" p ", "P C1 T1")]
        [DataRow("lpe", "LP-E C1 T1")]
        [DataRow("future", "C1 T1")]
        public void MemberLabelsNormalizeKnownTypesAndKeepUnknownTypesUnprefixed(string type, string expected)
        {
            var group = OsdSourceGroup.Build(new[] { Source(1, type), Source(2, null) }).Single();

            Assert.AreEqual(expected, group.CreateTile(1).GroupMembers[0].Label);
        }

        [TestMethod]
        public void RefreshUpdatesExistingTopologyWithoutReplacingCustomLabelsOrSelection()
        {
            using var host = new EditorHost("vertical", topology: false);
            var tile = host.Editor.SelectedElement;
            tile.GroupMembers[0].Label = "My preferred label";
            tile.GroupMembers[0].IsEnabled = false;
            host.Editor.Sources = Enumerable.Range(1, 16).Select(index => Source(index,
                index <= 6 ? "P" : index <= 14 ? "E" : "LPE")).ToArray();
            tile.GroupKey = OsdSourceGroup.Build(host.Editor.Sources).Single().Key;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            typeof(OsdEditorControl).GetMethod("OnRefreshGroupMembers", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host.Editor, new object[] { null, new RoutedEventArgs() });
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Settle();

            Assert.AreEqual(16, tile.GroupMembers.Count);
            Assert.AreEqual("My preferred label", tile.GroupMembers[0].Label);
            Assert.IsFalse(tile.GroupMembers[0].IsEnabled);
            Assert.AreEqual("P", tile.GroupMembers[0].CoreType);
            Assert.AreEqual(1, tile.GroupMembers[0].CoreIndex);
            Assert.AreEqual(1, tile.GroupMembers[0].ThreadIndex);
            Assert.AreEqual("LPE", tile.GroupMembers[15].CoreType);
            Assert.IsTrue(host.CoreTypes.IsVisible);
        }

        private static OsdTelemetrySource Source(int index, string type) => new OsdTelemetrySource
        {
            Key = "cpu.load." + index, Name = "CPU Core #" + index + " Thread #1",
            Category = "CPU", DeviceName = "Hybrid CPU", DeviceKey = "cpu",
            GroupKey = "cpu.loads", GroupName = "CPU loads", Unit = "%", GroupOrder = index,
            CoreType = type, CoreIndex = type == null ? null : index, ThreadIndex = type == null ? null : 1
        };

        private static void Settle()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(1) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }
            public GroupBarLayout Layout => Editor.Document.GetGroupBarLayout(Editor.SelectedElement);
            public ComboBox Rows => (ComboBox)Editor.FindName("GroupBarRows");
            public CheckBox CoreTypes => (CheckBox)Editor.FindName("GroupBarCoreTypes");

            public EditorHost(string orientation, bool topology = true)
            {
                Editor = new OsdEditorControl();
                var document = new OsdDesignDocument { Width = 600, Columns = 1, Padding = 12, CanvasHeight = 800, IsCanvas = true };
                double gap = orientation == "vertical" ? 4 : 8;
                var tile = new OsdDesignTile
                {
                    Kind = "group", Label = "CPU threads", GroupKey = "cpu.loads", GroupDisplay = "bar", GroupColumns = 8,
                    GroupBarOrientation = orientation == "vertical" ? "vertical" : null,
                    GroupShowLabels = false, GroupShowValues = false, Unit = "%", Minimum = 0, Maximum = 100,
                    GroupBarWidth = (576 - 7 * gap) / 8, GroupBarHeight = 12,
                    X = 0, Y = 0, FrameWidth = 600, FrameHeight = 256,
                    GroupMembers = Enumerable.Range(1, 16).Select(index => new OsdGroupMember
                    {
                        Key = "cpu.load." + index, Label = "C" + index, IsEnabled = true,
                        CoreType = !topology ? null : index <= 6 ? "P" : index <= 14 ? "E" : "LPE",
                        CoreIndex = topology ? index : null
                    }).ToList()
                };
                document.Tiles.Add(tile);
                document.EnsureGroupBarFits(tile);
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

            public void SeparateTypes(bool value)
            {
                CoreTypes.SetCurrentValue(CheckBox.IsCheckedProperty, value);
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
