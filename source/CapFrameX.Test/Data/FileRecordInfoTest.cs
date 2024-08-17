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
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
        }

        [TestMethod]
        public void RecordFileValidation_OCATCustomFilenameWithoutComment_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\CustomFilenameWithoutComment.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
            Assert.AreEqual(null, fileRecordInfo.Comment);
        }

        [TestMethod]
        public void RecordFileValidation_OCATCustomFilenameWithoutMetaDataInFilename_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\CustomFilenameWithoutMetaDataInFilename.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
        }

        [TestMethod]
        public void FileRecordInfo_OCATStandardFile_CorrectSystemInfo()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\OCAT-MetroExodus.exe-2019-02-20T101522.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.AreEqual("MetroExodus.exe", fileRecordInfo.GameName);
            Assert.AreEqual("MetroExodus.exe", fileRecordInfo.ProcessName);

            Assert.AreEqual("Micro-Star International Co. Ltd. MPG Z390 GAMING PRO CARBON (MS-7B17)", fileRecordInfo.MotherboardName);
            Assert.AreEqual("Windows 10 Pro 1809 (OS Build 17763.1.amd64fre.rs5_release.180914-1434)", fileRecordInfo.OsVersion);

            Assert.AreEqual("Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz", fileRecordInfo.ProcessorName);
            Assert.AreEqual("GeForce RTX 2080 Ti", fileRecordInfo.GraphicCardName);
            Assert.AreEqual("32 GB DDR4 3200 MT/s", fileRecordInfo.SystemRamInfo);
            Assert.AreEqual("Test", fileRecordInfo.Comment);
        }

        [TestMethod]
        public void RecordFileValidation_PresentMonTestOutputFile_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\PresentMonTestOutputFilename.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);

            Assert.AreEqual("FarCryNewDawn.exe", fileRecordInfo.GameName);
            Assert.AreEqual("FarCryNewDawn.exe", fileRecordInfo.ProcessName);
        }

        [TestMethod]
        public void RecordFileValidation_ShortFile_IsValidFalse()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\ShortFile.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsFalse(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
        }

        [TestMethod]
        public void RecordFileValidation_InvalidColumnFile_IsValidFalse()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\InvalidColumnFile.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsFalse(fileRecordInfo.IsValid);
            Assert.IsFalse(fileRecordInfo.HasInfoHeader);
        }

        [TestMethod]
        public void RecordFileValidation_CapFrameXFileWithHeader_IsValidTrue()
        {
            var fileInfo = new FileInfo(@"TestRecordFiles\CapFrameXFileWithHeader.csv");
            var fileRecordInfo = FileRecordInfo.Create(fileInfo, "testHash");

            Assert.IsTrue(fileRecordInfo.IsValid);
            Assert.IsTrue(fileRecordInfo.HasInfoHeader);
            Assert.AreEqual("re2.exe", fileRecordInfo.GameName);
            Assert.AreEqual("Resident Evil 2 Remake", fileRecordInfo.ProcessName);
            Assert.AreEqual("2019-03-30", fileRecordInfo.CreationDate);
            Assert.AreEqual("12:01:36", fileRecordInfo.CreationTime);
            Assert.AreEqual("ASUSTeK COMPUTER INC. ROG MAXIMUS XI HERO", fileRecordInfo.MotherboardName);
            Assert.AreEqual("Windows OS", fileRecordInfo.OsVersion);
            Assert.AreEqual("Intel(R) Core(TM) i9-9900K CPU @ 3.60GHz", fileRecordInfo.ProcessorName);
            Assert.AreEqual("NVIDIA GeForce RTX 2080 Ti", fileRecordInfo.GraphicCardName);
            Assert.AreEqual("32 GB 3800 MT/s", fileRecordInfo.SystemRamInfo);
            Assert.AreEqual("bla", fileRecordInfo.BaseDriverVersion);
            Assert.AreEqual("bla", fileRecordInfo.DriverPackage);
            Assert.AreEqual("1", fileRecordInfo.NumberGPUs);
            Assert.AreEqual("1920", fileRecordInfo.GPUCoreClock);
            Assert.AreEqual("7000", fileRecordInfo.GPUMemoryClock);
            Assert.AreEqual("11278", fileRecordInfo.GPUMemory);
            Assert.AreEqual("Test", fileRecordInfo.Comment);
        }
    }
}
