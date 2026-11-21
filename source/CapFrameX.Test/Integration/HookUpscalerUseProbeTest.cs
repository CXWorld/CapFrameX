using System;
using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.ExceptionServices;
using System.Threading;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The block in which the in-game hook publishes the DLSS and XeSS libraries it saw the game
    /// evaluate. Its native mirror is upscaler_runtime_use_test.cpp in the OSD repository.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class HookUpscalerUseProbeTest
    {
        private const int DlssSuperResolution = 0;
        private const int DlssRayReconstruction = 1;
        private const int Xess = 2;
        private static int _nextTestPid = 1620000000;

        [TestMethod]
        public void Layout_IsTheOneTheHookWrites()
        {
            Assert.AreEqual(72, HookUpscalerUseProbe.Size);
            Assert.AreEqual(0x31555543, HookUpscalerUseProbe.Magic);
            Assert.AreEqual(16, HookUpscalerUseProbe.PublicationOffset);
            Assert.AreEqual(24, HookUpscalerUseProbe.RuntimesOffset);
            Assert.AreEqual(16, HookUpscalerUseProbe.RuntimeSize);
            Assert.AreEqual(3, HookUpscalerUseProbe.RuntimeCount);
            Assert.AreEqual(0, HookUpscalerUseProbe.LastUseOffset);
            Assert.AreEqual(8, HookUpscalerUseProbe.VersionOffset);
            Assert.AreEqual(@"Local\CfxOsdHookUpscalerUseV1_4711", HookUpscalerUseProbe.MappingName(4711));
        }

        [TestMethod]
        public void TryParse_ReadsEveryUpscalerWithTheVersionOfItsLibrary()
        {
            byte[] block = Block(4711);
            WriteRuntime(block, DlssSuperResolution, 900100, 310, 7, 130, 0);
            WriteRuntime(block, DlssRayReconstruction, 900200, 310, 9, 16, 0);
            WriteRuntime(block, Xess, 900300, 2, 0, 2, 68);

            Assert.IsTrue(HookUpscalerUseProbe.TryParse(block, 4711, out var uses));

            Assert.AreEqual(900100UL, uses.DlssSuperResolution.LastUseTickMs);
            Assert.AreEqual("310.7.130", uses.DlssSuperResolution.Version.ToDisplayString());
            Assert.AreEqual(900200UL, uses.DlssRayReconstruction.LastUseTickMs);
            Assert.AreEqual("310.9.16", uses.DlssRayReconstruction.Version.ToDisplayString());
            Assert.AreEqual(900300UL, uses.Xess.LastUseTickMs);
            Assert.AreEqual("2.0.2", uses.Xess.Version.ToDisplayString());
            Assert.AreEqual(68, uses.Xess.Version.Revision);
        }

        [TestMethod]
        public void TryParse_ALibraryThatNeverEvaluatedReportsNothing()
        {
            Assert.IsTrue(HookUpscalerUseProbe.TryParse(Block(4711), 4711, out var uses));

            Assert.AreEqual(0UL, uses.DlssSuperResolution.LastUseTickMs);
            Assert.IsTrue(uses.DlssSuperResolution.Version.IsEmpty);
            Assert.AreEqual(0UL, uses.DlssRayReconstruction.LastUseTickMs);
            Assert.AreEqual(0UL, uses.Xess.LastUseTickMs);
        }

        [TestMethod]
        public void TryParse_ReadsTheLargestVersionComponents()
        {
            byte[] block = Block(4711);
            WriteRuntime(block, Xess, ulong.MaxValue, 65535, 65535, 65535, 65535);

            Assert.IsTrue(HookUpscalerUseProbe.TryParse(block, 4711, out var uses));

            Assert.AreEqual(ulong.MaxValue, uses.Xess.LastUseTickMs);
            Assert.AreEqual("65535.65535.65535", uses.Xess.Version.ToDisplayString());
            Assert.AreEqual(65535, uses.Xess.Version.Revision);
        }

        [TestMethod]
        public void TryParse_RejectsABlockThatIsNotThisProcessOrThisLayout()
        {
            Assert.IsFalse(HookUpscalerUseProbe.TryParse(Block(4711), 4712, out _),
                "A recycled name must not hand over another process' upscalers.");

            byte[] magic = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(magic, 0x31564643);
            Assert.IsFalse(HookUpscalerUseProbe.TryParse(magic, 4711, out _),
                "The block of the FidelityFX versions is another one.");

            byte[] version = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(version.AsSpan(4), 2);
            Assert.IsFalse(HookUpscalerUseProbe.TryParse(version, 4711, out _),
                "A later layout is not read with these offsets.");

            byte[] size = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(size.AsSpan(12), 88);
            Assert.IsFalse(HookUpscalerUseProbe.TryParse(size, 4711, out _));

            Assert.IsFalse(HookUpscalerUseProbe.TryParse(Block(4711).AsSpan(0, 71), 4711, out _));
        }

        [TestMethod]
        public void TryRead_MissingMappingReportsFalseWithoutFirstChanceException()
        {
            // The overlay refresh asks once per tick for every target, hooked or not.
            int pid = NextTestPid();
            int callingThread = Environment.CurrentManagedThreadId;
            int exceptions = 0;
            EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
            {
                if (Environment.CurrentManagedThreadId == callingThread) exceptions++;
            };

            bool success;
            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                success = HookUpscalerUseProbe.TryRead(pid, out _);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.IsFalse(success);
            Assert.AreEqual(0, exceptions);
            Assert.IsFalse(HookUpscalerUseProbe.TryRead(0, out _));
        }

        [TestMethod]
        public void TryRead_ReadsTheMappingOfTheHook()
        {
            int pid = NextTestPid();
            byte[] block = Block(pid);
            WriteRuntime(block, DlssSuperResolution, 900100, 310, 7, 130, 0);

            using MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookUpscalerUseProbe.MappingName(pid), HookUpscalerUseProbe.Size,
                MemoryMappedFileAccess.ReadWrite);
            using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, HookUpscalerUseProbe.Size, MemoryMappedFileAccess.ReadWrite);
            view.WriteArray(0, block, 0, block.Length);

            Assert.IsTrue(HookUpscalerUseProbe.TryRead(pid, out var uses));
            Assert.AreEqual(900100UL, uses.DlssSuperResolution.LastUseTickMs);
            Assert.AreEqual("310.7.130", uses.DlssSuperResolution.Version.ToDisplayString());

            // Odd while the hook is inside: a half-written version must not be shown.
            view.Write(HookUpscalerUseProbe.PublicationOffset, 7);
            Assert.IsFalse(HookUpscalerUseProbe.TryRead(pid, out _));
            view.Write(HookUpscalerUseProbe.PublicationOffset, 8);
            Assert.IsTrue(HookUpscalerUseProbe.TryRead(pid, out _));
        }

        internal static byte[] Block(int pid)
        {
            var block = new byte[HookUpscalerUseProbe.Size];
            BinaryPrimitives.WriteInt32LittleEndian(block, HookUpscalerUseProbe.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(4), HookUpscalerUseProbe.Version);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(8), pid);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(12), HookUpscalerUseProbe.Size);
            return block;
        }

        internal static void WriteRuntime(byte[] block, int index, ulong lastUseTickMs,
            ushort major, ushort minor, ushort build, ushort revision)
        {
            Span<byte> runtime = block.AsSpan(
                HookUpscalerUseProbe.RuntimesOffset + index * HookUpscalerUseProbe.RuntimeSize,
                HookUpscalerUseProbe.RuntimeSize);
            BinaryPrimitives.WriteUInt64LittleEndian(runtime, lastUseTickMs);
            Span<byte> version = runtime.Slice(HookUpscalerUseProbe.VersionOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(version, major);
            BinaryPrimitives.WriteUInt16LittleEndian(version.Slice(2), minor);
            BinaryPrimitives.WriteUInt16LittleEndian(version.Slice(4), build);
            BinaryPrimitives.WriteUInt16LittleEndian(version.Slice(6), revision);
        }

        private static int NextTestPid() => Interlocked.Increment(ref _nextTestPid);
    }
}
