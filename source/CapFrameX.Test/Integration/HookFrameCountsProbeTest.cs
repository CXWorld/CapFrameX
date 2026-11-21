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
    /// The block in which the in-game hook publishes the frames it saw reach the display and the
    /// frames the game rendered for its frame generation. Its native mirror is
    /// frame_generation_counts_test.cpp in the OSD repository.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class HookFrameCountsProbeTest
    {
        private static int _nextTestPid = 1630000000;

        [TestMethod]
        public void Layout_IsTheOneTheHookWrites()
        {
            Assert.AreEqual(80, HookFrameCountsProbe.Size);
            Assert.AreEqual(0x31464643, HookFrameCountsProbe.Magic);
            Assert.AreEqual(16, HookFrameCountsProbe.PublicationOffset);
            Assert.AreEqual(24, HookFrameCountsProbe.TickOffset);
            Assert.AreEqual(32, HookFrameCountsProbe.CountsOffset);
            Assert.AreEqual(6, HookFrameCountsProbe.CountCount);
            Assert.AreEqual(@"Local\CfxOsdHookFrameCountsV1_4711", HookFrameCountsProbe.MappingName(4711));
        }

        [TestMethod]
        public void TryParse_ReadsEveryCounterInTheOrderOfTheHook()
        {
            byte[] block = Block(4711, 900250, 360, 120, 121, 60, 59, 93);

            Assert.IsTrue(HookFrameCountsProbe.TryParse(block, 4711, out var counts));

            Assert.AreEqual(900250UL, counts.TickMs);
            Assert.AreEqual(360UL, counts.DisplayedFrames);
            Assert.AreEqual(120UL, counts.ProxyPresents);
            Assert.AreEqual(121UL, counts.StreamlineFrames);
            Assert.AreEqual(60UL, counts.FsrPreparedFrames);
            Assert.AreEqual(59UL, counts.FsrGeneratedFrames);
            Assert.AreEqual(93UL, counts.XessFrames);
        }

        [TestMethod]
        public void TryParse_ReadsCountersBeyondThirtyTwoBits()
        {
            // 500 displayed frames per second pass 32 bits after about a hundred days.
            byte[] block = Block(4711, ulong.MaxValue, 0x1_0000_0001, 0, ulong.MaxValue, 0, 0, 0);

            Assert.IsTrue(HookFrameCountsProbe.TryParse(block, 4711, out var counts));

            Assert.AreEqual(ulong.MaxValue, counts.TickMs);
            Assert.AreEqual(0x1_0000_0001UL, counts.DisplayedFrames);
            Assert.AreEqual(ulong.MaxValue, counts.StreamlineFrames);
        }

        [TestMethod]
        public void TryParse_RejectsABlockThatIsNotThisProcessOrThisLayout()
        {
            Assert.IsFalse(HookFrameCountsProbe.TryParse(Block(4711), 4712, out _),
                "A recycled name must not hand over another process' frames.");

            byte[] magic = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(magic, 0x31555543);
            Assert.IsFalse(HookFrameCountsProbe.TryParse(magic, 4711, out _),
                "The block of the upscaler libraries is another one.");

            byte[] version = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(version.AsSpan(4), 2);
            Assert.IsFalse(HookFrameCountsProbe.TryParse(version, 4711, out _),
                "A later layout is not read with these offsets.");

            byte[] size = Block(4711);
            BinaryPrimitives.WriteInt32LittleEndian(size.AsSpan(12), 88);
            Assert.IsFalse(HookFrameCountsProbe.TryParse(size, 4711, out _));

            Assert.IsFalse(HookFrameCountsProbe.TryParse(Block(4711).AsSpan(0, 79), 4711, out _));
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
                success = HookFrameCountsProbe.TryRead(pid, out _);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.IsFalse(success);
            Assert.AreEqual(0, exceptions);
            Assert.IsFalse(HookFrameCountsProbe.TryRead(0, out _));
        }

        [TestMethod]
        public void TryRead_ReadsTheMappingOfTheHook()
        {
            int pid = NextTestPid();
            byte[] block = Block(pid, 900250, 480, 120, 120, 0, 0, 0);

            using MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookFrameCountsProbe.MappingName(pid), HookFrameCountsProbe.Size,
                MemoryMappedFileAccess.ReadWrite);
            using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, HookFrameCountsProbe.Size, MemoryMappedFileAccess.ReadWrite);
            view.WriteArray(0, block, 0, block.Length);

            Assert.IsTrue(HookFrameCountsProbe.TryRead(pid, out var counts));
            Assert.AreEqual(480UL, counts.DisplayedFrames);
            Assert.AreEqual(120UL, counts.StreamlineFrames);

            // Odd while the hook is inside: counters of two moments must not be divided.
            view.Write(HookFrameCountsProbe.PublicationOffset, 7);
            Assert.IsFalse(HookFrameCountsProbe.TryRead(pid, out _));
            view.Write(HookFrameCountsProbe.PublicationOffset, 8);
            Assert.IsTrue(HookFrameCountsProbe.TryRead(pid, out _));
        }

        internal static byte[] Block(int pid, ulong tickMs = 0, params ulong[] counts)
        {
            var block = new byte[HookFrameCountsProbe.Size];
            BinaryPrimitives.WriteInt32LittleEndian(block, HookFrameCountsProbe.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(4), HookFrameCountsProbe.Version);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(8), pid);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(12), HookFrameCountsProbe.Size);
            BinaryPrimitives.WriteUInt64LittleEndian(block.AsSpan(HookFrameCountsProbe.TickOffset), tickMs);
            for (int i = 0; i < counts.Length; i++)
                BinaryPrimitives.WriteUInt64LittleEndian(
                    block.AsSpan(HookFrameCountsProbe.CountsOffset + i * sizeof(ulong)), counts[i]);
            return block;
        }

        private static int NextTestPid() => Interlocked.Increment(ref _nextTestPid);
    }
}
