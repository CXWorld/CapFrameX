using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The block in which the in-game hook publishes the FSR versions it saw the game dispatch.
    /// Its native mirror is fidelityfx_provider_version_test.cpp in the OSD repository.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class HookFidelityFxVersionProbeTest
    {
        private const uint Context = 1;
        private const uint LibraryDefault = 2;
        private static int _nextTestPid = 1610000000;

        [TestMethod]
        public void Layout_IsTheOneTheHookWrites()
        {
            Assert.AreEqual(136, HookFidelityFxVersionProbe.Size);
            Assert.AreEqual(0x31564643, HookFidelityFxVersionProbe.Magic);
            Assert.AreEqual(16, HookFidelityFxVersionProbe.PublicationOffset);
            Assert.AreEqual(24, HookFidelityFxVersionProbe.EffectsOffset);
            Assert.AreEqual(56, HookFidelityFxVersionProbe.EffectSize);
            Assert.AreEqual(0, HookFidelityFxVersionProbe.LastUseOffset);
            Assert.AreEqual(8, HookFidelityFxVersionProbe.VersionIdOffset);
            Assert.AreEqual(16, HookFidelityFxVersionProbe.SourceOffset);
            Assert.AreEqual(24, HookFidelityFxVersionProbe.NameOffset);
            Assert.AreEqual(32, HookFidelityFxVersionProbe.NameSize);
            Assert.AreEqual(@"Local\CfxOsdHookFidelityFxV1_4711", HookFidelityFxVersionProbe.MappingName(4711));
        }

        [TestMethod]
        public void TryParse_ReadsTheUpscalerAndTheFrameGeneration()
        {
            byte[] block = Block(4711);
            WriteEffect(block, 0, 900100, 0xF5A5CA1E00C01005, Context, "3.1.5");
            WriteEffect(block, 1, 900350, 0xF600000000401001, LibraryDefault, "1.1.1");

            Assert.IsTrue(HookFidelityFxVersionProbe.TryParse(block, 4711, out var versions));

            Assert.AreEqual(HookFidelityFxVersionSource.Context, versions.Upscaler.Source);
            Assert.AreEqual("3.1.5", versions.Upscaler.Name);
            Assert.AreEqual(0xF5A5CA1E00C01005UL, versions.Upscaler.VersionId);
            Assert.AreEqual(900100UL, versions.Upscaler.LastUseTickMs);
            Assert.AreEqual(HookFidelityFxVersionSource.LibraryDefault, versions.FrameGeneration.Source);
            Assert.AreEqual("1.1.1", versions.FrameGeneration.Name);
            Assert.AreEqual(900350UL, versions.FrameGeneration.LastUseTickMs);
        }

        [TestMethod]
        public void TryParse_ALibraryThatWasNeverDispatchedReportsNothing()
        {
            Assert.IsTrue(HookFidelityFxVersionProbe.TryParse(Block(4711), 4711, out var versions));

            Assert.AreEqual(HookFidelityFxVersionSource.None, versions.Upscaler.Source);
            Assert.IsNull(versions.Upscaler.Name);
            Assert.AreEqual(0UL, versions.Upscaler.LastUseTickMs);
            Assert.AreEqual(HookFidelityFxVersionSource.None, versions.FrameGeneration.Source);
        }

        [TestMethod]
        public void TryParse_RejectsABlockThatIsNotThisProcessOrThisLayout()
        {
            Assert.IsFalse(HookFidelityFxVersionProbe.TryParse(Block(4711), 4712, out _),
                "A recycled name must not hand over another process' versions.");

            byte[] magic = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(magic, 0x31534843);
            Assert.IsFalse(HookFidelityFxVersionProbe.TryParse(magic, 4711, out _));

            byte[] version = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(version.AsSpan(4), 2);
            Assert.IsFalse(HookFidelityFxVersionProbe.TryParse(version, 4711, out _),
                "A later layout is not read with these offsets.");

            byte[] size = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(size.AsSpan(12), 64);
            Assert.IsFalse(HookFidelityFxVersionProbe.TryParse(size, 4711, out _));

            Assert.IsFalse(HookFidelityFxVersionProbe.TryParse(Block(4711).AsSpan(0, 135), 4711, out _));
        }

        [TestMethod]
        public void TryParse_AVersionWithoutAUsableNameOrSourceIsNoVersion()
        {
            // The game's process writes the block: nothing in it is trusted as display text.
            byte[] block = Block(4711);
            WriteEffect(block, 0, 900100, 7, Context, "4.0\u0001");
            WriteEffect(block, 1, 900100, 8, 9, "4.0.2");
            Assert.IsTrue(HookFidelityFxVersionProbe.TryParse(block, 4711, out var versions));
            Assert.AreEqual(HookFidelityFxVersionSource.None, versions.Upscaler.Source);
            Assert.IsNull(versions.Upscaler.Name);
            Assert.AreEqual(900100UL, versions.Upscaler.LastUseTickMs, "The time of use still stands.");
            Assert.AreEqual(HookFidelityFxVersionSource.None, versions.FrameGeneration.Source,
                "A source this reader has no name for is not shown.");
            Assert.IsNull(versions.FrameGeneration.Name);

            WriteEffect(block, 0, 900100, 7, Context, "");
            Assert.IsTrue(HookFidelityFxVersionProbe.TryParse(block, 4711, out versions));
            Assert.AreEqual(HookFidelityFxVersionSource.None, versions.Upscaler.Source);
        }

        [TestMethod]
        public void ReadName_StopsAtTheTerminatorAndAtTheEndOfTheField()
        {
            var name = new byte[HookFidelityFxVersionProbe.NameSize];
            Encoding.ASCII.GetBytes("3.1.5").CopyTo(name, 0);
            name[6] = (byte)'x';
            Assert.AreEqual("3.1.5", HookFidelityFxVersionProbe.ReadName(name));

            Array.Fill(name, (byte)'9');
            Assert.AreEqual(new string('9', 31), HookFidelityFxVersionProbe.ReadName(name),
                "The last byte is the terminator, also when the hook did not write one.");

            Assert.IsNull(HookFidelityFxVersionProbe.ReadName(new byte[HookFidelityFxVersionProbe.NameSize]));
            name[3] = 0x7f;
            Assert.IsNull(HookFidelityFxVersionProbe.ReadName(name));
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
                success = HookFidelityFxVersionProbe.TryRead(pid, out _);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.IsFalse(success);
            Assert.AreEqual(0, exceptions);
            Assert.IsFalse(HookFidelityFxVersionProbe.TryRead(0, out _));
        }

        [TestMethod]
        public void TryRead_ReadsTheMappingOfTheHook()
        {
            int pid = NextTestPid();
            byte[] block = Block(pid);
            WriteEffect(block, 0, 900100, 0xF5A5CA1E00C01005, Context, "3.1.5");

            using MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookFidelityFxVersionProbe.MappingName(pid), HookFidelityFxVersionProbe.Size,
                MemoryMappedFileAccess.ReadWrite);
            using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, HookFidelityFxVersionProbe.Size, MemoryMappedFileAccess.ReadWrite);
            view.WriteArray(0, block, 0, block.Length);

            Assert.IsTrue(HookFidelityFxVersionProbe.TryRead(pid, out var versions));
            Assert.AreEqual("3.1.5", versions.Upscaler.Name);
            Assert.AreEqual(HookFidelityFxVersionSource.Context, versions.Upscaler.Source);

            // Odd while the hook is inside: a half-written name must not be shown.
            view.Write(HookFidelityFxVersionProbe.PublicationOffset, 7);
            Assert.IsFalse(HookFidelityFxVersionProbe.TryRead(pid, out _));
            view.Write(HookFidelityFxVersionProbe.PublicationOffset, 8);
            Assert.IsTrue(HookFidelityFxVersionProbe.TryRead(pid, out _));
        }

        internal static byte[] Block(int pid)
        {
            var block = new byte[HookFidelityFxVersionProbe.Size];
            BinaryPrimitives.WriteInt32LittleEndian(block, HookFidelityFxVersionProbe.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(4), HookFidelityFxVersionProbe.Version);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(8), pid);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(12), HookFidelityFxVersionProbe.Size);
            return block;
        }

        internal static void WriteEffect(byte[] block, int index, ulong lastUseTickMs, ulong versionId,
            uint source, string name)
        {
            Span<byte> effect = block.AsSpan(
                HookFidelityFxVersionProbe.EffectsOffset + index * HookFidelityFxVersionProbe.EffectSize,
                HookFidelityFxVersionProbe.EffectSize);
            effect.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(effect, lastUseTickMs);
            BinaryPrimitives.WriteUInt64LittleEndian(effect.Slice(HookFidelityFxVersionProbe.VersionIdOffset), versionId);
            BinaryPrimitives.WriteUInt32LittleEndian(effect.Slice(HookFidelityFxVersionProbe.SourceOffset), source);
            Encoding.Latin1.GetBytes(name).CopyTo(effect.Slice(HookFidelityFxVersionProbe.NameOffset));
        }

        private static int NextTestPid() => Interlocked.Increment(ref _nextTestPid);
    }
}
