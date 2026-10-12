using System;
using System.Linq;

using LibreHardwareMonitor.Hardware.Cpu;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class AmdCpuTopologyTest
    {
        private const uint TopologyLeaf = 0x80000026;

        [TestMethod]
        public void GetCcdCount_SmtSiblingsOnOneCcd_CountsOnce()
        {
            AmdCpuTopology[] topologies = Enumerable.Range(0, 16)
                .Select(apicId => ReadTopology((uint)apicId))
                .ToArray();

            Assert.AreEqual(1, AmdCpuTopology.GetCcdCount(topologies));
        }

        [TestMethod]
        public void GetCcdCount_TwoCcdsWithDisabledCores_CountsDistinctCcds()
        {
            uint[] apicIds = { 0, 1, 4, 5, 16, 17, 20, 21 };

            Assert.AreEqual(2, AmdCpuTopology.GetCcdCount(apicIds.Select(ReadTopology)));
        }

        [TestMethod]
        public void GetCcdCount_SmtDisabledAndNoncontiguousCcdIds_DoesNotInferCountFromLargestId()
        {
            uint[] apicIds = { 0, 2, 4, 0x30, 0x32, 0x34 };

            Assert.AreEqual(2, AmdCpuTopology.GetCcdCount(apicIds.Select(ReadTopology)));
        }

        [TestMethod]
        public void GetCcdCount_MoreThanEightCcdsWithHighApicIds_CountsAllVisibleCcds()
        {
            AmdCpuTopology[] topologies = Enumerable.Range(0, 12)
                .Select(ccd => ReadTopology(0x80000000U | ((uint)ccd << 4)))
                .ToArray();

            Assert.AreEqual(0x08000000U, topologies[0].CcdId);
            Assert.AreEqual(0x00800000U, topologies[0].SocketId);
            Assert.AreEqual(12, AmdCpuTopology.GetCcdCount(topologies));
        }

        [TestMethod]
        public void Read_TwoCcxWithinOneCcd_UsesTheCcdLevelShift()
        {
            Assert.AreEqual(0U, ReadTopology(0).CcdId);
            Assert.AreEqual(0U, ReadTopology(8).CcdId);
            Assert.AreEqual(1U, ReadTopology(16).CcdId);
        }

        [TestMethod]
        public void Read_CcxAndCcdHaveEqualShifts_AcceptsSingleCcxCcd()
        {
            var reader = new CpuidReader(
                Level(1, 2, 1, 16),
                Level(4, 16, 2, 16),
                Level(4, 16, 3, 16),
                Level(8, 32, 4, 16));

            Assert.AreEqual(1U, ReadTopology(reader).CcdId);
        }

        [TestMethod]
        public void Read_UnrelatedRegisterBits_DoNotChangeCcdId()
        {
            var reader = new CpuidReader(
                Level(0x12340004, 0xA5000010, 3, 0x80000010),
                Level(8, 32, 4, 0x80000010));

            Assert.AreEqual(0x08000001U, ReadTopology(reader).CcdId);
        }

        [TestMethod]
        [DataRow(Vendor.Intel, TopologyLeaf)]
        [DataRow(Vendor.Unknown, TopologyLeaf)]
        [DataRow(Vendor.AMD, 0x80000025U)]
        public void Read_UnsupportedCpu_DoesNotExecuteTopologyLeaf(Vendor vendor, uint maxExtendedLeaf)
        {
            var reader = new CpuidReader(Level(4, 16, 3, 0));

            Assert.IsNull(AmdCpuTopology.Read(vendor, maxExtendedLeaf, reader.Read));
            Assert.AreEqual(0, reader.ReadCount);
        }

        [TestMethod]
        public void Read_MissingCcdLevel_ReturnsUnknown()
        {
            var reader = new CpuidReader(
                Level(1, 2, 1, 0),
                Level(4, 16, 2, 0),
                Level(5, 32, 4, 0),
                Level(0, 0, 0, 0));

            Assert.IsNull(ReadTopology(reader));
        }

        [TestMethod]
        public void Read_MissingSocketLevel_ReturnsUnknown()
        {
            var reader = new CpuidReader(
                Level(1, 2, 1, 0),
                Level(4, 16, 3, 0),
                Level(0, 0, 0, 0));

            Assert.IsNull(ReadTopology(reader));
        }

        [TestMethod]
        [DataRow(0U, 16U)]
        [DataRow(2U, 0U)]
        [DataRow(2U, 0xA5000000U)]
        public void Read_InvalidLevelTerminatesEnumeration(uint levelType, uint processorCount)
        {
            var reader = new CpuidReader(
                Level(1, 2, 1, 0),
                Level(4, processorCount, levelType, 0),
                Level(4, 16, 3, 0),
                Level(8, 32, 4, 0));

            Assert.IsNull(ReadTopology(reader));
            Assert.AreEqual(2, reader.ReadCount);
        }

        [TestMethod]
        public void Read_CpuidReadFails_ReturnsUnknown()
        {
            var reader = new CpuidReader(Level(1, 2, 1, 0));

            Assert.IsNull(ReadTopology(reader));
            Assert.AreEqual(2, reader.ReadCount);
        }

        [TestMethod]
        public void Read_DecreasingShift_ReturnsUnknown()
        {
            var reader = new CpuidReader(
                Level(4, 16, 2, 0),
                Level(3, 8, 3, 0),
                Level(8, 32, 4, 0));

            Assert.IsNull(ReadTopology(reader));
        }

        [TestMethod]
        public void Read_InconsistentApicIds_ReturnsUnknown()
        {
            var reader = new CpuidReader(
                Level(1, 2, 1, 0x100),
                Level(4, 16, 3, 0x200),
                Level(8, 32, 4, 0x200));

            Assert.IsNull(ReadTopology(reader));
        }

        [TestMethod]
        public void Read_UnterminatedTopology_HasBoundedEnumeration()
        {
            var reader = new CpuidReader(
                Enumerable.Repeat(Level(1, 2, 1, 0), 257).ToArray());

            Assert.IsNull(ReadTopology(reader));
            Assert.AreEqual(256, reader.ReadCount);
        }

        [TestMethod]
        public void GetCcdCount_NoLogicalProcessors_ReturnsUnknown()
        {
            Assert.IsNull(AmdCpuTopology.GetCcdCount(Array.Empty<AmdCpuTopology>()));
        }

        [TestMethod]
        public void GetCcdCount_PartiallyMissingTopology_ReturnsUnknownInsteadOfUndercounting()
        {
            Assert.IsNull(AmdCpuTopology.GetCcdCount(new[] { ReadTopology(0), null, ReadTopology(16) }));
            Assert.IsNull(AmdCpuTopology.GetCcdCount(new AmdCpuTopology[] { null }));
        }

        [TestMethod]
        public void GetCcdCount_LogicalProcessorsFromDifferentSockets_ReturnsUnknown()
        {
            Assert.IsNull(AmdCpuTopology.GetCcdCount(new[] { ReadTopology(0), ReadTopology(0x100) }));
        }

        private static AmdCpuTopology ReadTopology(uint apicId)
        {
            return ReadTopology(new CpuidReader(
                Level(1, 2, 1, apicId),
                Level(3, 8, 2, apicId),
                Level(4, 16, 3, apicId),
                Level(8, 32, 4, apicId)));
        }

        private static AmdCpuTopology ReadTopology(CpuidReader reader)
        {
            return AmdCpuTopology.Read(Vendor.AMD, TopologyLeaf, reader.Read);
        }

        private static (uint Eax, uint Ebx, uint Ecx, uint Edx) Level(
            uint shift, uint processorCount, uint levelType, uint apicId)
        {
            return (shift, processorCount, levelType << 8, apicId);
        }

        private sealed class CpuidReader
        {
            private readonly (uint Eax, uint Ebx, uint Ecx, uint Edx)[] _levels;

            public CpuidReader(params (uint Eax, uint Ebx, uint Ecx, uint Edx)[] levels)
            {
                _levels = levels;
            }

            public int ReadCount { get; private set; }

            public bool Read(uint index, uint subleaf, out uint eax, out uint ebx, out uint ecx, out uint edx)
            {
                Assert.AreEqual(TopologyLeaf, index);
                Assert.AreEqual((uint)ReadCount, subleaf);
                ReadCount++;

                eax = ebx = ecx = edx = 0;
                if (subleaf >= _levels.Length)
                    return false;

                (eax, ebx, ecx, edx) = _levels[subleaf];
                ecx |= subleaf;
                return true;
            }
        }
    }
}
