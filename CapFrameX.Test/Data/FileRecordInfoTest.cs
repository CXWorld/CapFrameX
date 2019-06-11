using CapFrameX.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class FileRecordInfoTest
    {
        [TestMethod]
        public void RecordFileValidation_OCATStandardFile_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\OCAT-MetroExodus.exe-2019-02-20T101522.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo);

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
        }

        [TestMethod]
        public void RecordFileValidation_OCATCustomFilenameWithoutComment_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\CustomFilenameWithoutComment.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo);

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
            Assert.AreEqual(null, fileRecordInfo.Comment);
        }

        [TestMethod]
        public void RecordFileValidation_OCATCustomFilenameWithoutMetaDataInFilename_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\CustomFilenameWithoutMetaDataInFilename.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo);

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
        }

        [TestMethod]
        public void FileRecordInfo_OCATStandardFile_CorrectSystemInfo()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\OCAT-MetroExodus.exe-2019-02-20T101522.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo);

            Assert.AreEqual("MetroExodus.exe", fileRecordInfo.GameName);
            Assert.AreEqual("MetroExodus.exe", fileRecordInfo.ProcessName);

            Assert.AreEqual($"\"Micro-Star International Co. Ltd. MPG Z390 GAMING PRO CARBON (MS-7B17)\"", fileRecordInfo.MotherboardName);
            Assert.AreEqual($"\"Windows 10 Pro 1809 (OS Build 17763.1.amd64fre.rs5_release.180914-1434)\"", fileRecordInfo.OsVersion);

            Assert.AreEqual("Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz", fileRecordInfo.ProcessorName);
            Assert.AreEqual("GeForce RTX 2080 Ti", fileRecordInfo.GraphicCardName);
            Assert.AreEqual("32 GB DDR4 3200 MT/s", fileRecordInfo.SystemRamInfo);
            Assert.AreEqual("Test", fileRecordInfo.Comment);
        }
    }
}
