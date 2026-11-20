using System;
using System.IO;

namespace CapFrameX.Data
{
    /// <summary>
    /// Compares the folders of the record list by the folder a path names, however it is
    /// written: case, a trailing separator, forward slashes, <c>..</c> segments. The capture root
    /// and the observed folder come from the settings, the folders of the tree from the file
    /// system, so they stop being written alike once a settings file was edited or copied.
    /// </summary>
    public static class RecordFolderPath
    {
        /// <summary>True when both paths name the same folder.</summary>
        public static bool IsSameFolder(string left, string right)
        {
            string folder = Normalize(left);
            return folder != null && string.Equals(folder, Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when <paramref name="folder"/> is <paramref name="root"/> or lies below it.
        /// </summary>
        public static bool IsInFolder(string folder, string root)
        {
            string inner = Normalize(folder);
            string outer = Normalize(root);
            return inner != null && outer != null && inner.StartsWith(outer, StringComparison.OrdinalIgnoreCase);
        }

        // The full path with a trailing separator, so that "Captures2" does not lie in "Captures".
        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(path);
                return Path.EndsInDirectorySeparator(fullPath) ? fullPath : fullPath + Path.DirectorySeparatorChar;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
