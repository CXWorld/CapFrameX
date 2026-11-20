using CapFrameX.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// The folders of the record list are compared by the folder a path names: the capture root
    /// and the observed folder come from the settings, the folders of the tree from the file system.
    /// </summary>
    [TestClass]
    public class RecordFolderPathTest
    {
        [TestMethod]
        [DataRow(@"C:\Captures\Game", @"C:\Captures\Game")]
        [DataRow(@"C:\Captures\Game", @"c:\captures\GAME")]
        [DataRow(@"C:\Captures\Game", @"C:\Captures\Game\")]
        [DataRow(@"C:\Captures\Game", @"C:/Captures/Game")]
        [DataRow(@"C:\Captures\Game", @"C:\Captures\Other\..\Game")]
        [DataRow(@"D:\", @"d:\")]
        public void IsSameFolder_IgnoresHowAPathIsWritten(string left, string right)
        {
            Assert.IsTrue(RecordFolderPath.IsSameFolder(left, right));
            Assert.IsTrue(RecordFolderPath.IsSameFolder(right, left));
        }

        [TestMethod]
        [DataRow(@"C:\Captures\Game", @"C:\Captures")]
        [DataRow(@"C:\Captures\Game", @"C:\Captures\Game 2")]
        [DataRow(@"C:\Captures\Game", @"D:\Captures\Game")]
        [DataRow(@"C:\Captures\Game", "")]
        [DataRow(@"C:\Captures\Game", null)]
        [DataRow(null, null)]
        public void IsSameFolder_TellsOtherFoldersAndMissingPathsApart(string left, string right)
        {
            Assert.IsFalse(RecordFolderPath.IsSameFolder(left, right));
            Assert.IsFalse(RecordFolderPath.IsSameFolder(right, left));
        }

        [TestMethod]
        [DataRow(@"C:\Captures", @"C:\Captures")]
        [DataRow(@"C:\Captures\Game\Run", @"C:\Captures")]
        [DataRow(@"c:/captures/game", @"C:\Captures\")]
        [DataRow(@"D:\Captures", @"D:\")]
        public void IsInFolder_HoldsForTheRootAndTheFoldersBelowIt(string folder, string root)
        {
            Assert.IsTrue(RecordFolderPath.IsInFolder(folder, root));
        }

        [TestMethod]
        // The settings of an installed CapFrameX in a portable one (issue 485).
        [DataRow(@"C:\Users\User\Documents\CapFrameX\Captures", @"D:\CapFrameX\Portable\Captures")]
        [DataRow(@"C:\Captures", @"C:\Captures\Game")]
        [DataRow(@"C:\Captures 2", @"C:\Captures")]
        [DataRow(@"C:\Captures", "")]
        [DataRow(null, @"C:\Captures")]
        public void IsInFolder_FailsForFoldersElsewhere(string folder, string root)
        {
            Assert.IsFalse(RecordFolderPath.IsInFolder(folder, root));
        }
    }
}
