using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookDesignChannelTest
    {
        // Wire offsets are deliberately independent of the writer's constants. These are
        // the version-1 contract consumed by both the DXGI and Vulkan readers.
        private const int HeaderSize = 64;
        private const int PayloadCapacity = 1024 * 1024;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RejectedUpdateLeavesTheLastCompletePacketReadable(bool rejectTemplate)
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{\"scene\":\"유효\"}", "{\"load\":75}", 1, 1);
            var before = ReadPacket(fixture.Reader);
            string oversized = new string('x', PayloadCapacity + 1);

            Assert.ThrowsExactly<ArgumentException>(() => fixture.Channel.Publish(43,
                rejectTemplate ? oversized : "{\"scene\":\"new\"}",
                rejectTemplate ? "{\"load\":90}" : oversized, 2, 2));

            Assert.AreEqual(before, ReadPacket(fixture.Reader),
                "Rejecting either payload must not publish a partial scene, PID, revision or heartbeat.");
            fixture.Channel.Publish(43, "{\"scene\":\"new\"}", "{\"load\":90}", 2, 2);
            var recovered = ReadPacket(fixture.Reader);
            Assert.AreEqual(43, recovered.Pid);
            Assert.AreEqual("{\"load\":90}", recovered.Metrics);
            Assert.AreEqual(2UL, recovered.DesignRevision);
            Assert.AreEqual(2UL, recovered.MetricsRevision);
        }

        [TestMethod]
        public void InvalidUtf16CannotCorruptPreviouslyPublishedUtf8()
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{}", "{\"name\":\"GPU 🔥\"}", 1, 1);
            var before = ReadPacket(fixture.Reader);
            Assert.ThrowsExactly<EncoderFallbackException>(() =>
                fixture.Channel.Publish(42, "{}", "\uD800", 1, 2));
            Assert.AreEqual(before, ReadPacket(fixture.Reader));
        }

        [TestMethod]
        public void ShorterPayloadsPublishExactUtf8LengthsWithoutTrailingPreviousData()
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{\"scene\":\"" + new string('x', 4096) + "\"}",
                "{\"name\":\"" + new string('y', 4096) + "\"}", 1, 1);
            const string template = "{\"scene\":\"한국어 🔥\"}";
            const string metrics = "{\"温度\":72.5}";
            fixture.Channel.Publish(42, template, metrics, 2, 2);

            var packet = ReadPacket(fixture.Reader);
            Assert.AreEqual(template, packet.Template);
            Assert.AreEqual(metrics, packet.Metrics);
            Assert.AreEqual(StrictUtf8.GetByteCount(template), fixture.Reader.ReadInt32(20));
            Assert.AreEqual(StrictUtf8.GetByteCount(metrics), fixture.Reader.ReadInt32(24));
        }

        [TestMethod]
        public void IndependentRevisionsUpdateOnlyTheirPayloadAndHeartbeatReusesBoth()
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{\"scene\":1}", "{\"load\":10}", 1, 1);
            fixture.Channel.Publish(42, null, "{\"load\":20}", 1, 2);
            var metricsOnly = ReadPacket(fixture.Reader);
            Assert.AreEqual("{\"scene\":1}", metricsOnly.Template);
            Assert.AreEqual("{\"load\":20}", metricsOnly.Metrics);
            fixture.Channel.Publish(42, "{\"scene\":2}", null, 2, 2);
            var designOnly = ReadPacket(fixture.Reader);
            Assert.AreEqual("{\"scene\":2}", designOnly.Template);
            Assert.AreEqual(metricsOnly.Metrics, designOnly.Metrics);

            fixture.Channel.Publish(43, null, null, 2, 2);
            var heartbeat = ReadPacket(fixture.Reader);
            Assert.AreEqual(43, heartbeat.Pid);
            Assert.AreEqual(designOnly.Template, heartbeat.Template);
            Assert.AreEqual(designOnly.Metrics, heartbeat.Metrics);
            Assert.AreEqual(designOnly.DesignRevision, heartbeat.DesignRevision);
            Assert.AreEqual(designOnly.MetricsRevision, heartbeat.MetricsRevision);
            Assert.AreNotEqual(designOnly.Sequence, heartbeat.Sequence);
            Assert.IsTrue(heartbeat.Heartbeat >= designOnly.Heartbeat);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void InvalidTargetDisablesThePacketAndTheSameRevisionsCanResume(int pid)
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{\"scene\":1}", "{\"load\":10}", 7, 9);
            fixture.Channel.Publish(pid, null, null, 0, 0);
            AssertDisabled(fixture.Reader);
            fixture.Channel.Publish(43, null, null, 7, 9);
            var resumed = ReadPacket(fixture.Reader);
            Assert.AreEqual(43, resumed.Pid);
            Assert.AreEqual("{\"scene\":1}", resumed.Template);
            Assert.AreEqual("{\"load\":10}", resumed.Metrics);
        }

        [TestMethod]
        [DataRow(0UL, 1UL)]
        [DataRow(1UL, 0UL)]
        public void ZeroRevisionsAreRejectedWithoutAlteringThePublishedPacket(ulong designRevision, ulong metricsRevision)
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{}", "{}", 1, 1);
            var before = ReadPacket(fixture.Reader);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                fixture.Channel.Publish(42, "{}", "{}", designRevision, metricsRevision));
            Assert.AreEqual(before, ReadPacket(fixture.Reader));
        }

        [TestMethod]
        public void RestartedWriterResetsAnExistingMappingBeforePublishingNewContents()
        {
            using var fixture = new ChannelFixture();
            fixture.Channel.Publish(42, "{\"scene\":\"old\"}", "{\"load\":10}", 7, 9);
            fixture.Channel.Dispose();
            AssertDisabled(fixture.Reader);

            // The reader keeps the named section alive across writer restarts.
            using var restarted = HookDesignChannel.Create(fixture.Name);
            AssertDisabled(fixture.Reader);
            restarted.Publish(43, "{\"scene\":\"new\"}", "{\"load\":80}", 7, 9);
            var packet = ReadPacket(fixture.Reader);
            Assert.AreEqual(43, packet.Pid);
            Assert.AreEqual("{\"scene\":\"new\"}", packet.Template);
            Assert.AreEqual("{\"load\":80}", packet.Metrics);
        }

        private static void AssertDisabled(MemoryMappedViewAccessor reader)
        {
            Assert.AreEqual(0, reader.ReadInt32(8) & 1);
            foreach (int offset in new[] { 12, 16, 20, 24 }) Assert.AreEqual(0, reader.ReadInt32(offset));
            foreach (int offset in new[] { 32, 40, 48 }) Assert.AreEqual(0L, reader.ReadInt64(offset));
        }

        private static Packet ReadPacket(MemoryMappedViewAccessor reader)
        {
            Assert.AreEqual(0x31445843u, reader.ReadUInt32(0));
            Assert.AreEqual(1, reader.ReadInt32(4));
            int sequence = reader.ReadInt32(8);
            Assert.AreEqual(0, sequence & 1, "A completed publication must be readable by the seqlock reader.");
            Assert.AreEqual(1, reader.ReadInt32(16));
            Assert.AreEqual(0, reader.ReadInt32(28));
            Assert.AreEqual(0L, reader.ReadInt64(56));
            var packet = new Packet(sequence, reader.ReadInt32(12), reader.ReadInt64(32),
                reader.ReadUInt64(40), reader.ReadUInt64(48),
                ReadText(reader, HeaderSize, reader.ReadInt32(20)),
                ReadText(reader, HeaderSize + PayloadCapacity, reader.ReadInt32(24)));
            Assert.AreEqual(sequence, reader.ReadInt32(8));
            return packet;
        }

        private static string ReadText(MemoryMappedViewAccessor reader, int offset, int count)
        {
            Assert.IsTrue(count > 0 && count <= PayloadCapacity);
            var bytes = new byte[count];
            reader.ReadArray(offset, bytes, 0, count);
            return StrictUtf8.GetString(bytes);
        }

        private sealed record Packet(int Sequence, int Pid, long Heartbeat, ulong DesignRevision,
            ulong MetricsRevision, string Template, string Metrics);

        private sealed class ChannelFixture : IDisposable
        {
            public string Name { get; } = @"Local\CapFrameX-TileData-" + Guid.NewGuid().ToString("N");
            public HookDesignChannel Channel { get; }
            public MemoryMappedViewAccessor Reader { get; }
            private readonly MemoryMappedFile _mapping;

            public ChannelFixture()
            {
                Channel = HookDesignChannel.Create(Name);
                _mapping = MemoryMappedFile.OpenExisting(Name, MemoryMappedFileRights.Read);
                Reader = _mapping.CreateViewAccessor(0, HeaderSize + 2 * PayloadCapacity, MemoryMappedFileAccess.Read);
            }

            public void Dispose()
            {
                Channel.Dispose();
                Reader.Dispose();
                _mapping.Dispose();
            }
        }
    }
}
