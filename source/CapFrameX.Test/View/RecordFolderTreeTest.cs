using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CapFrameX.View.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.View
{
    /// <summary>
    /// The folder tree of the record list, built from real folders into a tree that is not on
    /// screen: the record list builds it at the start, while its popup is closed.
    /// </summary>
    [STATestClass]
    public class RecordFolderTreeTest
    {
        private string _folder;
        private string _root;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordFolderTreeTests", Guid.NewGuid().ToString("N"));
            _root = Path.Combine(_folder, "Captures");
            Directory.CreateDirectory(Path.Combine(_root, "Game A", "Run 1"));
            Directory.CreateDirectory(Path.Combine(_root, "Game B"));
            Directory.CreateDirectory(Path.Combine(_folder, "Elsewhere"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void Build_ShowsTheRootExpandedWithEveryFolderBelowIt()
        {
            var tree = new TreeView();
            tree.Items.Add(new TreeViewItem { Header = "The tree of the former root" });

            var root = RecordFolderTree.Build(tree, _root);

            Assert.AreEqual(1, tree.Items.Count);
            Assert.AreSame(root, tree.Items[0]);
            Assert.AreEqual("Captures", root.Header);
            Assert.IsTrue(root.IsExpanded);
            CollectionAssert.AreEqual(new[] { "Game A", "Game B" }, Headers(root));
            CollectionAssert.AreEqual(new[] { "Run 1" }, Headers((TreeViewItem)root.Items[0]));
            Assert.AreEqual(Path.Combine(_root, "Game A"), ((DirectoryInfo)((TreeViewItem)root.Items[0]).Tag).FullName);
        }

        [TestMethod]
        public void Find_ReturnsAFolderHoweverItsPathIsWritten()
        {
            var root = RecordFolderTree.Build(new TreeView(), _root);
            string written = _root.ToUpperInvariant().Replace('\\', '/') + "/game a/run 1/";

            Assert.AreEqual("Run 1", RecordFolderTree.Find(root, written)?.Header);
            Assert.AreSame(root, RecordFolderTree.Find(root, _root + Path.DirectorySeparatorChar));
        }

        [TestMethod]
        public void Find_ReturnsNullForAFolderTheTreeDoesNotHold()
        {
            // Issue 485: an observed folder that exists, but not below the capture root, as after
            // copying the settings of an installed CapFrameX into a portable one. The record list
            // rebuilt the tree until it found the folder, and the stack overflowed at every start.
            var root = RecordFolderTree.Build(new TreeView(), _root);

            Assert.IsNull(RecordFolderTree.Find(root, Path.Combine(_folder, "Elsewhere")));
            Assert.IsNull(RecordFolderTree.Find(root, _folder), "The folder above the root.");
            Assert.IsNull(RecordFolderTree.Find(root, Path.Combine(_root, "Deleted")));
            Assert.IsNull(RecordFolderTree.Find(root, ""));
            Assert.IsNull(RecordFolderTree.Find(root, null));
        }

        [TestMethod]
        public void Select_ExpandsTheFoldersAboveAndReportsTheItemToTheTree()
        {
            // The view observes the folder of the item the tree reports as selected.
            var tree = new TreeView();
            TreeViewItem reported = null;
            tree.AddHandler(TreeViewItem.SelectedEvent, new RoutedEventHandler((_, e) => reported = e.Source as TreeViewItem));
            var root = RecordFolderTree.Build(tree, _root);
            var run = RecordFolderTree.Find(root, Path.Combine(_root, "Game A", "Run 1"));

            RecordFolderTree.Select(run);

            Assert.AreSame(run, reported);
            Assert.AreSame(run, tree.SelectedItem);
            Assert.IsTrue(((TreeViewItem)run.Parent).IsExpanded);
            Assert.IsFalse(((TreeViewItem)root.Items[1]).IsExpanded);
        }

        private static string[] Headers(TreeViewItem item)
            => item.Items.Cast<TreeViewItem>().Select(child => (string)child.Header).ToArray();
    }
}
