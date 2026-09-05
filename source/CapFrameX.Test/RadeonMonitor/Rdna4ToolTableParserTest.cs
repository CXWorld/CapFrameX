using System;
using System.Globalization;
using System.Linq;
using CapFrameX.RadeonMonitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RadeonMainWindow = CapFrameX.RadeonMonitor.MainWindow;

namespace CapFrameX.Test.RadeonMonitor
{
    [TestClass]
    public class Rdna4ToolTableParserTest
    {
        private const string CoreCurrentName = "GPU Core Current (VDDCR_GFX)";

        [DataTestMethod]
        [DataRow(0x00660001, 0x294, 0x238, 0x188)]
        [DataRow(0x00660002, 0x294, 0x238, 0x188)]
        [DataRow(0x00660003, 0x294, 0x238, 0x188)]
        [DataRow(0x00660004, 0x290, 0x234, 0x184)]
        [DataRow(0x00660005, 0x1F8, 0x1CC, 0x11C)]
        [DataRow(0x00660006, 0x1F8, 0x1CC, 0x11C)]
        public void Parse_UsesFullVersionForCurrentAndClockOffsets(
            int version, int gfxOffset, int fclkOffset, int currentOffset)
        {
            uint[] dwords = new uint[gfxOffset / sizeof(uint) + 1];
            dwords[gfxOffset / sizeof(uint)] = BitConverter.SingleToUInt32Bits(2345.5f);
            dwords[fclkOffset / sizeof(uint)] = BitConverter.SingleToUInt32Bits(1234.25f);
            dwords[currentOffset / sizeof(uint)] = BitConverter.SingleToUInt32Bits(122.125f);

            RadeonToolTableTelemetry telemetry = Rdna4ToolTableParser.Parse(
                new RadeonToolTableSnapshot((uint)version, 0, 0, 0, dwords));
            MetricReading current = CoreCurrent(telemetry);

            Assert.AreEqual(3, telemetry.Readings.Count);
            Assert.AreEqual(0, telemetry.InvalidValueCount);
            Assert.AreEqual(2345.5, telemetry.Readings.Single(r => r.Name == "GPU Clock (Effective)").NumericValue);
            Assert.AreEqual(1234.25, telemetry.Readings.Single(r => r.Name == "GPU FCLK (Effective)").NumericValue);
            Assert.AreEqual(122.125, current.NumericValue);
            Assert.AreEqual("Current", current.Group);
            Assert.AreEqual("A", current.Unit);
            Assert.AreEqual("122.125", current.CurrentValue);
            Assert.AreEqual(3, current.DecimalPlaces);
            Assert.AreEqual($"+0x{currentOffset:X3}=0x42F44000", current.Raw);
        }

        [DataTestMethod]
        [DataRow(0.0f)]
        [DataRow(0.125f)]
        [DataRow(65.535f)]
        [DataRow(122.0f)]
        [DataRow(300.5f)]
        [DataRow(2000.0f)]
        public void Parse_CurrentIsAlreadyAmperesAndNotLimitedToPublicUint16(float amperes)
        {
            RadeonToolTableTelemetry telemetry = Rdna4ToolTableParser.Parse(Snapshot(amperes));
            MetricReading current = CoreCurrent(telemetry);

            Assert.AreEqual((double)amperes, current.NumericValue);
            Assert.AreEqual(((double)amperes).ToString("F3", CultureInfo.InvariantCulture), current.CurrentValue);
            Assert.AreEqual(0, telemetry.InvalidValueCount);
        }

        [DataTestMethod]
        [DataRow(float.NaN)]
        [DataRow(float.PositiveInfinity)]
        [DataRow(float.NegativeInfinity)]
        [DataRow(-1.0f)]
        [DataRow(2000.01f)]
        [DataRow(float.MaxValue)]
        public void Parse_InvalidCurrentRemainsUnavailableWithoutDiscardingClocks(float amperes)
        {
            RadeonToolTableTelemetry telemetry = Rdna4ToolTableParser.Parse(Snapshot(amperes));
            MetricReading current = CoreCurrent(telemetry);

            Assert.IsNull(current.NumericValue);
            Assert.AreEqual("\u2014", current.CurrentValue);
            Assert.AreEqual($"+0x11C=0x{BitConverter.SingleToUInt32Bits(amperes):X8}", current.Raw);
            Assert.AreEqual(3, telemetry.Readings.Count);
            Assert.AreEqual(1, telemetry.InvalidValueCount);
            Assert.IsTrue(telemetry.Readings.Where(r => r.Group == "Clocks").All(r => r.NumericValue.HasValue));
        }

        [DataTestMethod]
        [DataRow(0x00660000)]
        [DataRow(0x00660007)]
        [DataRow(0x004E000C)]
        public void Parse_RejectsUnverifiedFullVersion(int version)
        {
            Assert.ThrowsException<NotSupportedException>(() =>
                Rdna4ToolTableParser.Parse(Snapshot(122.0f) with { Version = (uint)version }));
        }

        [DataTestMethod]
        [DataRow(0x00660001, 0x294)]
        [DataRow(0x00660002, 0x294)]
        [DataRow(0x00660003, 0x294)]
        [DataRow(0x00660004, 0x290)]
        [DataRow(0x00660005, 0x1F8)]
        [DataRow(0x00660006, 0x1F8)]
        public void Parse_RejectsTruncatedTable(int version, int lastOffset)
        {
            Assert.ThrowsException<ArgumentException>(() => Rdna4ToolTableParser.Parse(
                new RadeonToolTableSnapshot((uint)version, 0, 0, 0, new uint[lastOffset / sizeof(uint)])));
        }

        [TestMethod]
        public void Parse_RejectsNullSnapshotAndData()
        {
            Assert.ThrowsException<ArgumentNullException>(() => Rdna4ToolTableParser.Parse(null));
            Assert.ThrowsException<ArgumentNullException>(() =>
                Rdna4ToolTableParser.Parse(Snapshot(122.0f) with { Dwords = null }));
        }

        [TestMethod]
        public void CreateUnavailable_PreservesAllSensorIdentitiesBeforeFirstSuccessfulRead()
        {
            RadeonToolTableTelemetry unavailable = Rdna4ToolTableParser.CreateUnavailable();
            RadeonToolTableTelemetry valid = Rdna4ToolTableParser.Parse(Snapshot(122.0f));

            Assert.AreEqual(3, unavailable.InvalidValueCount);
            CollectionAssert.AreEqual(
                valid.Readings.Select(r => (r.Group, r.Name, r.Unit, r.DecimalPlaces)).ToArray(),
                unavailable.Readings.Select(r => (r.Group, r.Name, r.Unit, r.DecimalPlaces)).ToArray());
            Assert.IsTrue(unavailable.Readings.All(r =>
                r.NumericValue == null && r.CurrentValue == "\u2014" && r.Raw == "unavailable"));
        }

        [TestMethod]
        public void MergeToolTableReadings_ReplacesOnlyGfxCurrentAndRetainsComplementaryValues()
        {
            MetricReading[] driverReadings =
            {
                Reading("Current", "GFX current", 43.923),
                Reading("Current", "SOC current", 12.0),
                Reading("Power", "Total board power", 338.0),
                Reading("Voltage", "GFX voltage", 1087.0)
            };
            RadeonToolTableTelemetry tool = Rdna4ToolTableParser.Parse(Snapshot(122.0f));

            var merged = RadeonMainWindow.MergeToolTableReadings(driverReadings, tool.Readings, RadeonGeneration.Rdna4);

            Assert.AreEqual(6, merged.Count);
            Assert.IsFalse(merged.Any(r => r.Name == "GFX current"));
            Assert.AreEqual(122.0, merged.Single(r => r.Name == CoreCurrentName).NumericValue);
            CollectionAssert.IsSubsetOf(driverReadings.Skip(1).ToArray(), merged.ToArray());
            Assert.AreEqual(merged.Count, merged.Select(r => (r.Group, r.Name)).Distinct().Count());
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MergeToolTableReadings_DoesNotSubstituteDriverCurrentForInvalidOrMissingPrivateData(bool missing)
        {
            MetricReading[] driverReadings = { Reading("Current", "GFX current", 43.923) };
            RadeonToolTableTelemetry tool = missing
                ? Rdna4ToolTableParser.CreateUnavailable()
                : Rdna4ToolTableParser.Parse(Snapshot(float.NaN));

            var merged = RadeonMainWindow.MergeToolTableReadings(driverReadings, tool.Readings, RadeonGeneration.Rdna4);

            Assert.IsFalse(merged.Any(r => r.Name == "GFX current"));
            Assert.IsNull(merged.Single(r => r.Name == CoreCurrentName).NumericValue);
        }

        [TestMethod]
        public void Statistics_IgnoreInvalidCurrentAndRetainAmperesAbove65()
        {
            MetricStatisticsTracker tracker = new MetricStatisticsTracker();
            tracker.Update(Rdna4ToolTableParser.Parse(Snapshot(122.0f)).Readings);
            tracker.Update(Rdna4ToolTableParser.Parse(Snapshot(float.NaN)).Readings);
            tracker.Update(Rdna4ToolTableParser.CreateUnavailable().Readings);

            MetricReading current = tracker.Update(Rdna4ToolTableParser.Parse(Snapshot(246.0f)).Readings)
                .Single(r => r.Name == CoreCurrentName);

            Assert.AreEqual("122.000", current.MinimumValue);
            Assert.AreEqual("246.000", current.MaximumValue);
            Assert.AreEqual("184.000", current.AverageValue);
        }

        private static RadeonToolTableSnapshot Snapshot(float amperes)
        {
            uint[] dwords = new uint[2048];
            dwords[0x11C / sizeof(uint)] = BitConverter.SingleToUInt32Bits(amperes);
            dwords[0x1F8 / sizeof(uint)] = BitConverter.SingleToUInt32Bits(2345.5f);
            dwords[0x1CC / sizeof(uint)] = BitConverter.SingleToUInt32Bits(1234.25f);
            return new RadeonToolTableSnapshot(0x00660006, 0, 0, 0, dwords);
        }

        private static MetricReading CoreCurrent(RadeonToolTableTelemetry telemetry)
        {
            return telemetry.Readings.Single(r => r.Name == CoreCurrentName);
        }

        private static MetricReading Reading(string group, string name, double value)
        {
            return new MetricReading(group, name, value.ToString(CultureInfo.InvariantCulture), "", "", value);
        }
    }
}
