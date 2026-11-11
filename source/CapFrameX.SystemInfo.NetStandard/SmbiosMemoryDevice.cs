using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.SystemInfo.NetStandard
{
    /// <summary>
    /// An installed memory module as the firmware describes it in an SMBIOS memory device
    /// structure (type 17). Besides the strings <c>Win32_PhysicalMemory</c> also reports, it carries
    /// the module manufacturer ID copied from SPD, which identifies the vendor even when the
    /// firmware's own vendor table does not know it and reports "Unknown".
    /// </summary>
    public sealed class SmbiosMemoryDevice
    {
        private const byte MemoryDeviceType = 17;
        private const byte EndOfTableType = 127;
        private const int SizeOffset = 0x0C;
        private const int ManufacturerOffset = 0x17;
        private const int PartNumberOffset = 0x1A;
        // SMBIOS 3.2+: "the two-byte module manufacturer ID found in the SPD; LSB first".
        private const int ModuleManufacturerIdOffset = 0x2C;

        /// <summary>Creates a module description.</summary>
        public SmbiosMemoryDevice(string manufacturer, string partNumber, ushort moduleManufacturerId)
        {
            Manufacturer = manufacturer ?? string.Empty;
            PartNumber = partNumber ?? string.Empty;
            ModuleManufacturerId = moduleManufacturerId;
        }

        /// <summary>The manufacturer string, as <c>Win32_PhysicalMemory</c> reports it.</summary>
        public string Manufacturer { get; }

        /// <summary>The part number string, as <c>Win32_PhysicalMemory</c> reports it.</summary>
        public string PartNumber { get; }

        /// <summary>
        /// The JEDEC module manufacturer ID from SPD: the number of continuation codes in the low
        /// byte, the manufacturer code in the high byte. 0 when the firmware does not know it or
        /// predates SMBIOS 3.2.
        /// </summary>
        public ushort ModuleManufacturerId { get; }

        /// <summary>
        /// Reads the installed modules from a raw SMBIOS structure table, as
        /// <c>MSSmBios_RawSMBiosTables.SMBiosData</c> returns it. Empty slots are skipped, and a
        /// truncated or malformed table yields the modules read up to that point.
        /// </summary>
        public static IReadOnlyList<SmbiosMemoryDevice> Parse(byte[] table)
        {
            var devices = new List<SmbiosMemoryDevice>();
            if (table == null)
                return devices;

            int offset = 0;
            while (offset + 4 <= table.Length)
            {
                byte type = table[offset];
                byte length = table[offset + 1];
                if (type == EndOfTableType || length < 4 || offset + length > table.Length)
                    break;

                var strings = ReadStrings(table, offset + length, out int next);

                if (type == MemoryDeviceType && IsInstalled(table, offset, length))
                {
                    devices.Add(new SmbiosMemoryDevice(
                        GetString(table, offset, length, ManufacturerOffset, strings),
                        GetString(table, offset, length, PartNumberOffset, strings),
                        length >= ModuleManufacturerIdOffset + 2
                            ? BitConverter.ToUInt16(table, offset + ModuleManufacturerIdOffset)
                            : (ushort)0));
                }

                offset = next;
            }

            return devices;
        }

        private static bool IsInstalled(byte[] table, int offset, byte length)
        {
            // Size 0 marks an empty slot; 0xFFFF (unknown) and 0x7FFF (extended size) are modules.
            return length >= SizeOffset + 2 && BitConverter.ToUInt16(table, offset + SizeOffset) != 0;
        }

        private static string GetString(byte[] table, int offset, byte length, int field, List<string> strings)
        {
            if (length <= field)
                return string.Empty;

            // String fields hold a 1-based index into the strings that follow the structure.
            int index = table[offset + field];
            return index > 0 && index <= strings.Count ? strings[index - 1].Trim() : string.Empty;
        }

        private static List<string> ReadStrings(byte[] table, int start, out int next)
        {
            var strings = new List<string>();
            int position = start;

            // A structure without strings still ends with two null bytes.
            if (position + 1 < table.Length && table[position] == 0 && table[position + 1] == 0)
            {
                next = position + 2;
                return strings;
            }

            while (position < table.Length && table[position] != 0)
            {
                int end = position;
                while (end < table.Length && table[end] != 0)
                    end++;

                strings.Add(Encoding.ASCII.GetString(table, position, end - position));
                position = end + 1;
            }

            next = position + 1;
            return strings;
        }
    }
}
