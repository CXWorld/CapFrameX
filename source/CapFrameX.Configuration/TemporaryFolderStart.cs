using System;
using System.IO;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Recognizes a start from a temporary folder. Archive programs run an executable from an
    /// archive by extracting the archive into one and delete it again once they consider the
    /// program finished. WinRAR did so 3 s after starting CapFrameX, which was still running
    /// (crash report of 2026-11-10): its settings could no longer be written, and the libraries it
    /// had not loaded yet were gone.
    /// </summary>
    public static class TemporaryFolderStart
    {
        // The default temporary folder of every user. These names are never shortened to 8.3
        // names, so the long path of the application always contains them.
        private static readonly string DefaultTemporarySegment =
            Path.DirectorySeparatorChar + Path.Combine("AppData", "Local", "Temp") + Path.DirectorySeparatorChar;

        /// <summary>
        /// True when <paramref name="directory"/> lies in the temporary folder of the current
        /// user or in the default temporary folder of any user.
        /// </summary>
        public static bool IsTemporaryFolder(string directory)
            => IsTemporaryFolder(directory, Path.GetTempPath());

        /// <summary>
        /// True when <paramref name="directory"/> lies in <paramref name="temporaryPath"/> or in
        /// the default temporary folder of any user (<c>…\AppData\Local\Temp\…</c>), which also
        /// covers an elevation under another account, whose temporary folder differs from the one
        /// the archive program used.
        /// </summary>
        public static bool IsTemporaryFolder(string directory, string temporaryPath)
        {
            string folder = Normalize(directory);
            if (folder == null)
                return false;

            if (folder.IndexOf(DefaultTemporarySegment, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string temporaryFolder = Normalize(temporaryPath);
            if (temporaryFolder == null)
                return false;

            // A TEMP variable that names a drive root would make every folder of the drive temporary.
            if (string.Equals(temporaryFolder, Normalize(Path.GetPathRoot(temporaryFolder)), StringComparison.OrdinalIgnoreCase))
                return false;

            return folder.StartsWith(temporaryFolder, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(path.Trim());
                return fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? fullPath
                    : fullPath + Path.DirectorySeparatorChar;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
