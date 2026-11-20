using CapFrameX.Data;
using System.IO;
using System.Windows.Controls;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// The folder tree of the record list: the capture root and every folder below it. Each item
    /// carries its folder as a <see cref="DirectoryInfo"/> in its tag and shows the folder's name.
    /// </summary>
    public static class RecordFolderTree
    {
        /// <summary>
        /// Replaces the items of <paramref name="tree"/> by the root folder, expanded, with the
        /// folders below it.
        /// </summary>
        /// <returns>The item of the root folder.</returns>
        public static TreeViewItem Build(TreeView tree, string rootDirectory)
        {
            tree.Items.Clear();

            var root = CreateItem(new DirectoryInfo(rootDirectory));
            root.IsExpanded = true;
            tree.Items.Add(root);
            return root;
        }

        /// <summary>
        /// The item of <paramref name="directory"/> among <paramref name="item"/> and the items
        /// below it, or null: the tree only holds folders that exist below its root.
        /// </summary>
        public static TreeViewItem Find(TreeViewItem item, string directory)
        {
            if (item.Tag is DirectoryInfo folder && RecordFolderPath.IsSameFolder(folder.FullName, directory))
                return item;

            foreach (var child in item.Items)
            {
                if (child is TreeViewItem childItem && Find(childItem, directory) is TreeViewItem found)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Selects <paramref name="item"/>, expands the folders above it and scrolls to it.
        /// </summary>
        public static void Select(TreeViewItem item)
        {
            for (var parent = item.Parent as TreeViewItem; parent != null; parent = parent.Parent as TreeViewItem)
                parent.IsExpanded = true;

            item.BringIntoView();
            item.IsSelected = true;
        }

        private static TreeViewItem CreateItem(DirectoryInfo directory)
        {
            // DirectoryInfo.ToString() returns the full path on .NET Core+
            // (it returned just the name on .NET Framework), so use Name here.
            var item = new TreeViewItem
            {
                Header = directory.Name,
                Tag = directory
            };

            try
            {
                foreach (DirectoryInfo subDirectory in directory.GetDirectories())
                    item.Items.Add(CreateItem(subDirectory));
            }
            // A folder that cannot be listed is shown without subfolders.
            catch { }

            return item;
        }
    }
}
