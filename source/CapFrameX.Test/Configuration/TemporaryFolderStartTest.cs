using CapFrameX.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class TemporaryFolderStartTest
    {
        [TestMethod]
        // WinRAR's folder for an executable started from an archive (crash report of 2026-11-10).
        [DataRow(@"C:\Users\dilly\AppData\Local\Temp\Rar$EXa19332.46968.rartemp\", @"C:\Users\dilly\AppData\Local\Temp\")]
        // Elevated under another account, whose TEMP differs from the archive program's.
        [DataRow(@"C:\Users\dilly\AppData\Local\Temp\7zO8A2C1B3E\CapFrameX\", @"C:\Users\Admin\AppData\Local\Temp\")]
        // GetTempPath may return 8.3 names, the application directory never does.
        [DataRow(@"C:\Users\CapFrameX\AppData\Local\Temp\Rar$EXa1.2.rartemp\", @"C:\Users\CAPFRA~1\AppData\Local\Temp\")]
        [DataRow(@"D:\Temp\Rar$EXa1.2.rartemp\", @"D:\Temp\")]
        [DataRow(@"d:\temp\Unpacked", @"D:\TEMP")]
        [DataRow(@"D:\Temp\", @"D:\Temp\")]
        public void FoldersInATemporaryFolderAreTemporary(string directory, string temporaryPath)
        {
            Assert.IsTrue(TemporaryFolderStart.IsTemporaryFolder(directory, temporaryPath));
        }

        [TestMethod]
        [DataRow(@"C:\Users\dilly\Downloads\CapFrameX\", @"C:\Users\dilly\AppData\Local\Temp\")]
        [DataRow(@"C:\Program Files\CapFrameX\", @"C:\Users\dilly\AppData\Local\Temp\")]
        [DataRow(@"D:\Temporary\CapFrameX\", @"D:\Temp\")]
        [DataRow(@"C:\Users\dilly\AppData\Local\TempFiles\CapFrameX\", @"D:\Temp\")]
        // A TEMP variable that names a drive root makes nothing on that drive temporary.
        [DataRow(@"D:\Tools\CapFrameX\", @"D:\")]
        [DataRow(@"D:\Tools\CapFrameX\", null)]
        [DataRow(null, @"D:\Temp\")]
        public void OtherFoldersAreNotTemporary(string directory, string temporaryPath)
        {
            Assert.IsFalse(TemporaryFolderStart.IsTemporaryFolder(directory, temporaryPath));
        }
    }
}
