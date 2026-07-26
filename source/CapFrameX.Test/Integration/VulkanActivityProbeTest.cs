using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.ExceptionServices;
using System.Threading;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    [DoNotParallelize]
    public class VulkanActivityProbeTest
    {
        private const long StateSize = 24;
        private static int _nextTestPid = 1500000000;

        [TestMethod]
        public void TryRead_MissingMappingReturnsNoLayerWithoutFirstChanceException()
        {
            int pid = NextTestPid();
            int callingThread = Environment.CurrentManagedThreadId;
            int fileNotFoundExceptions = 0;
            EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
            {
                if (Environment.CurrentManagedThreadId == callingThread &&
                    args.Exception is FileNotFoundException)
                    fileNotFoundExceptions++;
            };

            bool success;
            VulkanActivitySnapshot snapshot;
            string error;
            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                success = VulkanActivityProbe.TryRead(pid, out snapshot, out error);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.IsTrue(success);
            Assert.IsFalse(snapshot.IsLayerLoaded);
            Assert.IsNull(error);
            Assert.AreEqual(0, fileNotFoundExceptions);
        }

        [TestMethod]
        public void TryRead_ReadsExistingPerProcessRendererState()
        {
            int pid = NextTestPid();
            const long lastVulkanPresentTickMs = 123456789;
            const int preferredBackend = 1;

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                VulkanActivityProbe.GetMappingName(pid), StateSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StateSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, 2);
                view.Write(8, lastVulkanPresentTickMs);
                view.Write(16, preferredBackend);
                view.Flush();

                bool success = VulkanActivityProbe.TryRead(
                    pid, out VulkanActivitySnapshot snapshot, out string error);

                Assert.IsTrue(success);
                Assert.IsNull(error);
                Assert.IsTrue(snapshot.IsLayerLoaded);
                Assert.AreEqual(lastVulkanPresentTickMs, snapshot.LastVulkanPresentTickMs);
                Assert.AreEqual(preferredBackend, snapshot.PreferredBackend);
                // Nothing written at offset 20 -> the layer has not measured an extent yet.
                Assert.AreEqual(0, snapshot.ResolutionX);
                Assert.AreEqual(0, snapshot.ResolutionY);
            }
        }

        /// <summary>
        /// The extent shares the 24-byte block's trailing padding, packed as
        /// <c>width | height &lt;&lt; 16</c>. Keeping the block at 24 bytes is what lets an older
        /// hook and a newer layer (or the reverse) still map the same arbitration state.
        /// </summary>
        [TestMethod]
        public void TryRead_UnpacksTheSwapchainExtentFromTheStatePadding()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                VulkanActivityProbe.GetMappingName(pid), StateSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StateSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, 2);
                view.Write(8, 42L);
                view.Write(20, unchecked((int)((1440u << 16) | 3440u)));
                view.Flush();

                Assert.IsTrue(VulkanActivityProbe.TryRead(
                    pid, out VulkanActivitySnapshot snapshot, out _));
                Assert.AreEqual(3440, snapshot.ResolutionX);
                Assert.AreEqual(1440, snapshot.ResolutionY);
            }
        }

        [TestMethod]
        public void TryRead_UnpacksAnExtentWhoseHeightSetsTheSignBit()
        {
            // A height above 32767 sets bit 31 of the packed word. Reading it as a signed int
            // must not sign-extend into a negative height.
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                VulkanActivityProbe.GetMappingName(pid), StateSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StateSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, 2);
                view.Write(8, 42L);
                view.Write(20, unchecked((int)((43200u << 16) | 7680u)));
                view.Flush();

                Assert.IsTrue(VulkanActivityProbe.TryRead(
                    pid, out VulkanActivitySnapshot snapshot, out _));
                Assert.AreEqual(7680, snapshot.ResolutionX);
                Assert.AreEqual(43200, snapshot.ResolutionY);
            }
        }

        [TestMethod]
        public void TryRead_DetectsMappingCreatedAfterInitialMiss()
        {
            int pid = NextTestPid();

            Assert.IsTrue(VulkanActivityProbe.TryRead(
                pid, out VulkanActivitySnapshot missing, out string missingError));
            Assert.IsFalse(missing.IsLayerLoaded);
            Assert.IsNull(missingError);

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                VulkanActivityProbe.GetMappingName(pid), StateSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StateSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, 2);
                view.Write(8, 42L);
                view.Flush();

                Assert.IsTrue(VulkanActivityProbe.TryRead(
                    pid, out VulkanActivitySnapshot present, out string presentError));
                Assert.IsTrue(present.IsLayerLoaded);
                Assert.AreEqual(42L, present.LastVulkanPresentTickMs);
                Assert.IsNull(presentError);
            }
        }

        private static int NextTestPid()
            => Interlocked.Increment(ref _nextTestPid);
    }
}
