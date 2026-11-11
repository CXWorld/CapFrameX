using CapFrameX.Contracts.Configuration;
using CapFrameX.Mcp.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;

namespace CapFrameX.Test.Mcp
{
    /// <summary>
    /// The MCP record tools read the folder the record list shows, also in portable mode.
    /// </summary>
    [TestClass]
    public class RecordToolsDirectoryTest
    {
        private const string DefaultCaptures = @"MyDocuments\CapFrameX\Captures";
        private const string PortableCaptures = @"D:\CapFrameX Portable\Portable\Captures";

        [TestMethod]
        public void PortableMode_DefaultFolder_IsThePortableCapturesFolder()
        {
            var tools = CreateTools(DefaultCaptures);

            Assert.AreEqual(Path.GetFullPath(PortableCaptures), tools.ResolveRecordsDirectory().FullName);
        }

        [TestMethod]
        public void OtherDocumentsFolder_IsStillResolvedUnderDocuments()
        {
            var tools = CreateTools(@"MyDocuments\Benchmarks");

            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(documents, "Benchmarks")), tools.ResolveRecordsDirectory().FullName);
        }

        [TestMethod]
        public void AbsoluteFolder_IsUsedAsIs()
        {
            var tools = CreateTools(@"E:\Captures\Cyberpunk");

            Assert.AreEqual(Path.GetFullPath(@"E:\Captures\Cyberpunk"), tools.ResolveRecordsDirectory().FullName);
        }

        [TestMethod]
        public void NoFolder_IsNull()
        {
            Assert.IsNull(CreateTools(null).ResolveRecordsDirectory());
            Assert.IsNull(CreateTools(string.Empty).ResolveRecordsDirectory());
        }

        private static RecordTools CreateTools(string observedDirectory)
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupGet(config => config.ObservedDirectory).Returns(observedDirectory);

            // As PathService does in portable mode: only the default placeholders are mapped.
            var pathService = new Mock<IPathService>();
            pathService.Setup(paths => paths.ResolveDocumentsPlaceholder(It.IsAny<string>()))
                .Returns((string path) => path != null && path.Contains(DefaultCaptures) ? PortableCaptures : path);

            return new RecordTools(configuration.Object, null, pathService.Object);
        }
    }
}
