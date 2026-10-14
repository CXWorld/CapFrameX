using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CapFrameX.SystemInfo.NetStandard;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class SmbiosMemoryDeviceTest
    {
        // Formatted-area lengths: SMBIOS 3.2 is the first with the module manufacturer ID.
        private const byte Smbios32Length = 0x54;
        private const byte Smbios28Length = 0x28;

        [TestMethod]
        public void Parse_ReadsInstalledModulesAndSkipsEmptySlots()
        {
            var table = Concat(
                Structure(0, 0x18, "American Megatrends Inc.", "1.20"),
                MemoryDevice(Smbios32Length, 16384, "Unknown", "F4-3000C15-16GVR", 0xCD04),
                MemoryDevice(Smbios32Length, 0, "NO DIMM", "NO DIMM", 0),
                MemoryDevice(Smbios32Length, 16384, "G Skill Intl", "F4-3000C15-16GVR  ", 0xCD04),
                EndOfTable());

            var devices = SmbiosMemoryDevice.Parse(table);

            Assert.AreEqual(2, devices.Count);
            Assert.AreEqual("Unknown", devices[0].Manufacturer);
            Assert.AreEqual("F4-3000C15-16GVR", devices[0].PartNumber);
            Assert.AreEqual((ushort)0xCD04, devices[0].ModuleManufacturerId);
            Assert.AreEqual("G Skill Intl", devices[1].Manufacturer);
            Assert.AreEqual("F4-3000C15-16GVR", devices[1].PartNumber);
        }

        [TestMethod]
        public void Parse_RecordBeforeSmbios32_HasNoModuleManufacturerId()
        {
            var table = Concat(MemoryDevice(Smbios28Length, 8192, "Kingston", "KHX2400C15/8G", 0), EndOfTable());

            var device = SmbiosMemoryDevice.Parse(table).Single();

            Assert.AreEqual("Kingston", device.Manufacturer);
            Assert.AreEqual("KHX2400C15/8G", device.PartNumber);
            Assert.AreEqual((ushort)0, device.ModuleManufacturerId);
        }

        [TestMethod]
        public void Parse_ExtendedOrUnknownSize_CountsAsInstalled()
        {
            var table = Concat(
                MemoryDevice(Smbios32Length, 0x7FFF, "Samsung", "M323R4GA3BB0-CQKOD", 0xCE80),
                MemoryDevice(Smbios32Length, 0xFFFF, "Micron", "MTC8C1084S1SC48BA1", 0x2C80),
                EndOfTable());

            Assert.AreEqual(2, SmbiosMemoryDevice.Parse(table).Count);
        }

        [TestMethod]
        public void Parse_StructureWithoutStrings_IsSkipped()
        {
            // Physical memory array (type 16) carries no strings: its set is just two null bytes.
            var table = Concat(
                Structure(16, 0x17),
                MemoryDevice(Smbios32Length, 16384, "Corsair", "CMK16GX4M2B3200C16", 0x9E02),
                EndOfTable());

            Assert.AreEqual("Corsair", SmbiosMemoryDevice.Parse(table).Single().Manufacturer);
        }

        [TestMethod]
        public void Parse_StringIndexOutOfRange_YieldsEmptyString()
        {
            var device = MemoryDevice(Smbios32Length, 16384, "Corsair", "CMK16GX4M2B3200C16", 0x9E02);
            device[0x17] = 9;

            var parsed = SmbiosMemoryDevice.Parse(Concat(device, EndOfTable())).Single();

            Assert.AreEqual(string.Empty, parsed.Manufacturer);
            Assert.AreEqual("CMK16GX4M2B3200C16", parsed.PartNumber);
        }

        [TestMethod]
        public void Parse_StopsAtEndOfTable()
        {
            var table = Concat(
                MemoryDevice(Smbios32Length, 16384, "Kingston", "KF432C16BB/8", 0x9801),
                EndOfTable(),
                MemoryDevice(Smbios32Length, 16384, "Corsair", "CMK16GX4M2B3200C16", 0x9E02));

            Assert.AreEqual("Kingston", SmbiosMemoryDevice.Parse(table).Single().Manufacturer);
        }

        [TestMethod]
        public void Parse_TruncatedTable_ReturnsModulesReadSoFar()
        {
            var first = MemoryDevice(Smbios32Length, 16384, "Kingston", "KF432C16BB/8", 0x9801);
            var second = MemoryDevice(Smbios32Length, 16384, "Corsair", "CMK16GX4M2B3200C16", 0x9E02);
            var table = Concat(first, second.Take(0x20).ToArray());

            Assert.AreEqual("Kingston", SmbiosMemoryDevice.Parse(table).Single().Manufacturer);
        }

        [TestMethod]
        public void Parse_NullOrEmptyTable_ReturnsNoModules()
        {
            Assert.AreEqual(0, SmbiosMemoryDevice.Parse(null).Count);
            Assert.AreEqual(0, SmbiosMemoryDevice.Parse(new byte[0]).Count);
        }

        private static byte[] MemoryDevice(byte length, ushort size, string manufacturer, string partNumber,
            ushort moduleManufacturerId)
        {
            var formatted = new byte[length];
            formatted[0] = 17;
            formatted[1] = length;
            BitConverter.GetBytes(size).CopyTo(formatted, 0x0C);
            formatted[0x17] = 1;
            formatted[0x1A] = 2;
            if (length >= 0x2E)
                BitConverter.GetBytes(moduleManufacturerId).CopyTo(formatted, 0x2C);

            return Concat(formatted, StringSet(manufacturer, partNumber));
        }

        private static byte[] Structure(byte type, byte length, params string[] strings)
        {
            var formatted = new byte[length];
            formatted[0] = type;
            formatted[1] = length;

            return Concat(formatted, StringSet(strings));
        }

        private static byte[] EndOfTable() => Structure(127, 4);

        private static byte[] StringSet(params string[] strings)
        {
            var bytes = new List<byte>();
            foreach (var value in strings)
            {
                bytes.AddRange(Encoding.ASCII.GetBytes(value));
                bytes.Add(0);
            }

            // The set ends with an extra null byte; without strings it is two null bytes.
            bytes.Add(0);
            if (strings.Length == 0)
                bytes.Add(0);

            return bytes.ToArray();
        }

        private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();
    }
}
