using System.Buffers.Binary;
using System.Globalization;
using System.IO;

namespace CapFrameX.RadeonMonitor
{
    internal static class MetricsParser
    {
        private static readonly string[] Rdna2ClockNames =
        {
            "GFXCLK", "SOCCLK", "UCLK", "FCLK", "DCLK0", "VCLK0", "DCLK1",
            "VCLK1", "DCEFCLK", "DISPCLK", "PIXCLK", "PHYCLK", "DTBCLK"
        };

        private static readonly string[] Rdna3ClockNames =
        {
            "GFXCLK", "SOCCLK", "UCLK", "FCLK", "DCLK0", "VCLK0", "DCLK1",
            "VCLK1", "DISPCLK", "DPPCLK", "DPREFCLK", "DCFCLK", "DTBCLK"
        };

        private static readonly string[] Rdna4ClockNames =
        {
            "GFXCLK", "SOCCLK", "UCLK", "FCLK", "DCLK0", "VCLK0", "DISPCLK",
            "DPPCLK", "DPREFCLK", "DCFCLK", "DTBCLK"
        };

        private static readonly string[] D3HotModeNames = { "BACO", "MSR", "BAMACO", "ULPS" };

        private static readonly string[] Rdna2ThrottlerNames =
        {
            "Padding", "Temperature edge", "Temperature hotspot", "Temperature memory",
            "Temperature VR GFX", "Temperature VR memory 0", "Temperature VR memory 1",
            "Temperature VR SOC", "Temperature liquid 0", "Temperature liquid 1",
            "Temperature PLX", "TDC GFX", "TDC SOC", "PPT0", "PPT1", "PPT2",
            "PPT3", "FIT", "PPM", "APCC"
        };

        private static readonly string[] Rdna3ThrottlerNames =
        {
            "Temperature edge", "Temperature hotspot", "Temperature hotspot GFX",
            "Temperature hotspot memory", "Temperature memory", "Temperature VR GFX",
            "Temperature VR memory 0", "Temperature VR memory 1", "Temperature VR SOC",
            "Temperature VR U", "Temperature liquid 0", "Temperature liquid 1",
            "Temperature PLX", "TDC GFX", "TDC SOC", "TDC U", "PPT0", "PPT1",
            "PPT2", "PPT3", "FIT", "GFX APCC+"
        };

        private static readonly string[] Rdna4ThrottlerNames =
        {
            "Temperature edge", "Temperature hotspot", "Temperature hotspot GFX",
            "Temperature hotspot SOC", "Temperature memory", "Temperature VR GFX",
            "Temperature VR SOC", "Temperature VR memory 0", "Temperature VR memory 1",
            "Temperature liquid 0", "Temperature liquid 1", "Temperature PLX", "TDC GFX",
            "TDC SOC", "PPT0", "PPT1", "PPT2", "PPT3", "FIT", "GFX APCC+", "GFX DVO"
        };

        public static IReadOnlyList<MetricReading> Parse(
            uint[] dwords,
            RadeonGeneration generation,
            Rdna2MetricsLayout rdna2Layout,
            Rdna3MetricsLayout rdna3Layout)
        {
            ArgumentNullException.ThrowIfNull(dwords);

            byte[] bytes = new byte[dwords.Length * sizeof(uint)];
            for (int i = 0; i < dwords.Length; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint)), dwords[i]);
            }

            return generation switch
            {
                RadeonGeneration.Rdna2 => ParseRdna2(bytes, rdna2Layout),
                RadeonGeneration.Rdna3 => ParseRdna3(bytes, rdna3Layout),
                RadeonGeneration.Rdna4 => ParseRdna4(bytes),
                _ => throw new ArgumentOutOfRangeException(nameof(generation))
            };
        }

        private static IReadOnlyList<MetricReading> ParseRdna2(
            byte[] data,
            Rdna2MetricsLayout layout)
        {
            if (layout == Rdna2MetricsLayout.Auto)
            {
                throw new ArgumentException("The RDNA2 metrics layout must be resolved before parsing.", nameof(layout));
            }

            MetricsReader reader = new(data);
            MetricBuilder metrics = new();

            ReadCurrentClocks(reader, metrics, Rdna2ClockNames);
            metrics.AddFrequency("Average clocks", "GFXCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "GFXCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "FCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "FCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "UCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "UCLK after deep sleep", reader.ReadUInt16());

            metrics.AddPercent("Activity", "GFX activity", reader.ReadUInt16());
            metrics.AddPercent("Activity", "UCLK activity", reader.ReadUInt16());

            AddRdna2Voltage(metrics, "SOC voltage", reader.ReadByte());
            AddRdna2Voltage(metrics, "GFX voltage", reader.ReadByte());
            AddRdna2Voltage(metrics, "Memory VID", reader.ReadByte());
            reader.Skip(1);

            metrics.AddInteger("Power", "Average socket power", reader.ReadUInt16(), "W");

            string[] temperatureNames =
            {
                "Edge", "Hotspot", "Memory", "VR GFX", "VR memory 0", "VR memory 1",
                "VR SOC", "Liquid 0", "Liquid 1", "PLX"
            };
            ReadTemperatures(reader, metrics, temperatureNames);
            reader.Skip(2);

            if (layout == Rdna2MetricsLayout.Base)
            {
                metrics.AddHex("Throttling", "Throttler status", reader.ReadUInt32());
            }
            else
            {
                metrics.AddInteger("Counters", "Metrics accumulator", reader.ReadUInt32(), string.Empty);
                ReadThrottlingPercentages(reader, metrics, Rdna2ThrottlerNames);
            }

            metrics.AddInteger("PCI Express", "Link DPM level", reader.ReadByte(), "level");
            metrics.AddFanPwm(reader.ReadByte());
            metrics.AddInteger("Fan", "Fan speed", reader.ReadUInt16(), "RPM");

            ReadByteD3HotCounters(reader, metrics);
            metrics.AddInteger("Counters", "Energy accumulator", reader.ReadUInt32(), "raw");
            metrics.AddFrequency("Video", "Average VCLK0", reader.ReadUInt16());
            metrics.AddFrequency("Video", "Average DCLK0", reader.ReadUInt16());
            metrics.AddFrequency("Video", "Average VCLK1", reader.ReadUInt16());
            metrics.AddFrequency("Video", "Average DCLK1", reader.ReadUInt16());

            if (layout is Rdna2MetricsLayout.V3 or Rdna2MetricsLayout.V4)
            {
                metrics.AddPercent("Video", "VCN 0 activity", reader.ReadUInt16());
                metrics.AddPercent("Video", "VCN 1 activity", reader.ReadUInt16());
            }
            else
            {
                metrics.AddPercent("Video", "VCN activity", reader.ReadUInt16());
            }

            metrics.AddPcieRate(reader.ReadByte(), isZeroBased: true);
            metrics.AddPcieWidth(reader.ReadByte());
            metrics.AddFrequency("Average clocks", "GFXCLK target", reader.ReadUInt16());

            switch (layout)
            {
                case Rdna2MetricsLayout.Base:
                case Rdna2MetricsLayout.V2:
                    reader.Skip(2);
                    break;
                case Rdna2MetricsLayout.V3:
                    metrics.AddSerial(reader.ReadUInt32(), reader.ReadUInt32());
                    break;
                case Rdna2MetricsLayout.V4:
                    metrics.AddInteger("SmartShift", "APU STAPM SmartShift limit", reader.ReadByte(), "%");
                    metrics.AddInteger("SmartShift", "Average APU socket power", reader.ReadByte(), "W");
                    metrics.AddInteger("SmartShift", "APU STAPM limit", reader.ReadByte(), "%");
                    reader.Skip(1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layout));
            }

            int expectedSize = layout switch
            {
                Rdna2MetricsLayout.Base => 136,
                Rdna2MetricsLayout.V2 => 156,
                Rdna2MetricsLayout.V3 => 164,
                Rdna2MetricsLayout.V4 => 160,
                _ => throw new ArgumentOutOfRangeException(nameof(layout))
            };
            reader.AssertPosition(expectedSize, $"RDNA2 {layout}");
            return metrics.Items;
        }

        private static IReadOnlyList<MetricReading> ParseRdna3(
            byte[] data,
            Rdna3MetricsLayout layout)
        {
            if (layout == Rdna3MetricsLayout.Auto)
            {
                throw new ArgumentException("The RDNA3 metrics layout must be resolved before parsing.", nameof(layout));
            }

            MetricsReader reader = new(data);
            MetricBuilder metrics = new();

            ReadCurrentClocks(reader, metrics, Rdna3ClockNames);
            ReadCommonRdna3And4Averages(reader, metrics, "PCIe busy");
            metrics.AddInteger("Counters", "Metrics counter", reader.ReadUInt32(), string.Empty);

            ReadVoltageAndCurrentPlanes(
                reader,
                metrics,
                new[] { "GFX", "SOC", "VMEMP", "VDDIO memory", "U" });

            metrics.AddPercent("Activity", "GFX activity", reader.ReadUInt16());
            metrics.AddPercent("Activity", "UCLK activity", reader.ReadUInt16());
            metrics.AddPercent("Video", "VCN 0 activity", reader.ReadUInt16());
            metrics.AddPercent("Video", "VCN 1 activity", reader.ReadUInt16());
            metrics.AddInteger("Counters", "Energy accumulator", reader.ReadUInt32(), "raw");
            metrics.AddInteger("Power", "Average socket power", reader.ReadUInt16(), "W");
            metrics.AddInteger("Power", "Average total board power", reader.ReadUInt16(), "W");

            ReadTemperatures(
                reader,
                metrics,
                new[]
                {
                    "Edge", "Hotspot", "Hotspot GFX", "Hotspot memory", "Memory", "VR GFX",
                    "VR memory 0", "VR memory 1", "VR SOC", "VR U", "Liquid 0", "Liquid 1", "PLX"
                });
            metrics.AddInteger("Temperature", "Fan intake", reader.ReadUInt16(), "°C");

            metrics.AddPcieRate(reader.ReadByte(), isZeroBased: false);
            metrics.AddPcieWidth(reader.ReadByte());
            metrics.AddFanPwm(reader.ReadByte());
            reader.Skip(1);
            metrics.AddInteger("Fan", "Fan speed", reader.ReadUInt16(), "RPM");

            ReadThrottlingPercentages(reader, metrics, Rdna3ThrottlerNames);
            if (layout == Rdna3MetricsLayout.Smu13_0_0)
            {
                metrics.AddPercent("Throttling", "Vmax", reader.ReadByte());
                reader.Skip(3);
            }
            ReadD3HotCounters(reader, metrics);

            metrics.AddInteger("SmartShift", "APU STAPM SmartShift limit", reader.ReadUInt16(), "W");
            metrics.AddInteger("SmartShift", "APU STAPM limit", reader.ReadUInt16(), "W");
            metrics.AddInteger("SmartShift", "Average APU socket power", reader.ReadUInt16(), "W");
            metrics.AddPercent("Activity", "Maximum UCLK activity", reader.ReadUInt16());
            metrics.AddSerial(reader.ReadUInt32(), reader.ReadUInt32());

            int expectedSize = layout == Rdna3MetricsLayout.Smu13_0_0 ? 244 : 240;
            reader.AssertPosition(expectedSize, $"RDNA3 {layout}");
            return metrics.Items;
        }

        private static IReadOnlyList<MetricReading> ParseRdna4(byte[] data)
        {
            MetricsReader reader = new(data);
            MetricBuilder metrics = new();

            ReadCurrentClocks(reader, metrics, Rdna4ClockNames);
            ReadCommonRdna3And4Averages(reader, metrics, "Average PCIe busy");

            metrics.AddFrequency("Moving averages", "GFXCLK target", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "GFXCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "GFXCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "FCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "FCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "UCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "UCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "VCLK0", reader.ReadUInt16());
            metrics.AddFrequency("Moving averages", "DCLK0", reader.ReadUInt16());
            metrics.AddPercent("Moving averages", "GFX activity", reader.ReadUInt16());
            metrics.AddPercent("Moving averages", "UCLK activity", reader.ReadUInt16());
            metrics.AddPercent("Moving averages", "VCN 0 activity", reader.ReadUInt16());
            metrics.AddPercent("Moving averages", "PCIe busy", reader.ReadUInt16());
            metrics.AddPercent("Moving averages", "Maximum UCLK activity", reader.ReadUInt16());
            metrics.AddInteger("Moving averages", "Socket power", reader.ReadUInt16(), "W");
            reader.Skip(2);

            metrics.AddInteger("Counters", "Metrics counter", reader.ReadUInt32(), string.Empty);
            ReadVoltageAndCurrentPlanes(
                reader,
                metrics,
                new[] { "GFX", "SOC", "VDDCI memory", "VDDIO memory" });

            metrics.AddPercent("Activity", "GFX activity", reader.ReadUInt16());
            metrics.AddPercent("Activity", "UCLK activity", reader.ReadUInt16());
            metrics.AddPercent("Video", "VCN 0 activity", reader.ReadUInt16());
            metrics.AddPercent("Video", "VCN 1 activity", reader.ReadUInt16());
            metrics.AddInteger("Counters", "Energy accumulator", reader.ReadUInt32(), "raw");
            metrics.AddInteger("Power", "Average socket power", reader.ReadUInt16(), "W");
            metrics.AddInteger("Power", "Average total board power", reader.ReadUInt16(), "W");

            ReadTemperatures(
                reader,
                metrics,
                new[]
                {
                    "Edge", "Hotspot", "Hotspot GFX", "Hotspot SOC", "Memory", "VR GFX",
                    "VR SOC", "VR memory 0", "VR memory 1", "Liquid 0", "Liquid 1", "PLX"
                });
            metrics.AddInteger("Temperature", "Fan intake", reader.ReadUInt16(), "°C");

            metrics.AddPcieRate(reader.ReadByte(), isZeroBased: false);
            metrics.AddPcieWidth(reader.ReadByte());
            metrics.AddFanPwm(reader.ReadByte());
            reader.Skip(1);
            metrics.AddInteger("Fan", "Fan speed", reader.ReadUInt16(), "RPM");

            ReadThrottlingPercentages(reader, metrics, Rdna4ThrottlerNames);
            metrics.AddPercent("Throttling", "Vmax", reader.ReadByte());
            reader.Skip(2);
            ReadD3HotCounters(reader, metrics);

            metrics.AddInteger("SmartShift", "APU STAPM SmartShift limit", reader.ReadUInt16(), "W");
            metrics.AddInteger("SmartShift", "APU STAPM limit", reader.ReadUInt16(), "W");
            metrics.AddInteger("SmartShift", "Average APU socket power", reader.ReadUInt16(), "W");
            metrics.AddPercent("Activity", "Maximum UCLK activity", reader.ReadUInt16());
            metrics.AddSerial(reader.ReadUInt32(), reader.ReadUInt32());

            reader.AssertPosition(260, "RDNA4");
            return metrics.Items;
        }

        private static void ReadCurrentClocks(
            MetricsReader reader,
            MetricBuilder metrics,
            IReadOnlyList<string> names)
        {
            foreach (string name in names)
            {
                metrics.AddFrequency("Current clocks", name, reader.ReadUInt32());
            }
        }

        private static void ReadCommonRdna3And4Averages(
            MetricsReader reader,
            MetricBuilder metrics,
            string pcieBusyName)
        {
            metrics.AddFrequency("Average clocks", "GFXCLK target", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "GFXCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "GFXCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "FCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "FCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "UCLK before deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "UCLK after deep sleep", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "VCLK0", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "DCLK0", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "VCLK1", reader.ReadUInt16());
            metrics.AddFrequency("Average clocks", "DCLK1", reader.ReadUInt16());
            metrics.AddPercent("PCI Express", pcieBusyName, reader.ReadUInt16());
            metrics.AddInteger("Power", "dGPU W_MAX", reader.ReadUInt16(), "W");
            reader.Skip(2);
        }

        private static void ReadVoltageAndCurrentPlanes(
            MetricsReader reader,
            MetricBuilder metrics,
            IReadOnlyList<string> planeNames)
        {
            foreach (string plane in planeNames)
            {
                metrics.AddInteger("Voltage", $"Average {plane} voltage", reader.ReadUInt16(), "mV");
            }

            foreach (string plane in planeNames)
            {
                metrics.AddInteger("Current", $"Average {plane} current", reader.ReadUInt16(), "A");
            }
        }

        private static void ReadTemperatures(
            MetricsReader reader,
            MetricBuilder metrics,
            IReadOnlyList<string> names)
        {
            foreach (string name in names)
            {
                metrics.AddInteger("Temperature", name, reader.ReadUInt16(), "°C");
            }
        }

        private static void ReadThrottlingPercentages(
            MetricsReader reader,
            MetricBuilder metrics,
            IReadOnlyList<string> names)
        {
            foreach (string name in names)
            {
                metrics.AddPercent("Throttling", name, reader.ReadByte());
            }
        }

        private static void ReadByteD3HotCounters(MetricsReader reader, MetricBuilder metrics)
        {
            foreach (string mode in D3HotModeNames)
            {
                metrics.AddInteger("D3Hot", $"{mode} entry count", reader.ReadByte(), "count");
            }

            foreach (string mode in D3HotModeNames)
            {
                metrics.AddInteger("D3Hot", $"{mode} exit count", reader.ReadByte(), "count");
            }

            foreach (string mode in D3HotModeNames)
            {
                metrics.AddInteger("D3Hot", $"{mode} ARM messages", reader.ReadByte(), "count");
            }
        }

        private static void ReadD3HotCounters(MetricsReader reader, MetricBuilder metrics)
        {
            foreach (string mode in D3HotModeNames)
            {
                metrics.AddInteger("D3Hot", $"{mode} entry count", reader.ReadUInt32(), "count");
            }

            foreach (string mode in D3HotModeNames)
            {
                metrics.AddInteger("D3Hot", $"{mode} exit count", reader.ReadUInt32(), "count");
            }

            foreach (string mode in D3HotModeNames)
            {
                metrics.AddInteger("D3Hot", $"{mode} ARM messages", reader.ReadUInt32(), "count");
            }
        }

        private static void AddRdna2Voltage(MetricBuilder metrics, string name, byte raw)
        {
            double millivolts = 1550.0 - (6.25 * raw);
            metrics.AddScaled("Voltage", name, millivolts, "mV", raw, "F2");
        }

        private sealed class MetricsReader
        {
            private readonly byte[] data;
            private int position;

            public MetricsReader(byte[] data)
            {
                this.data = data;
            }

            public byte ReadByte()
            {
                EnsureAvailable(1);
                return data[position++];
            }

            public ushort ReadUInt16()
            {
                EnsureAvailable(sizeof(ushort));
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position));
                position += sizeof(ushort);
                return value;
            }

            public uint ReadUInt32()
            {
                EnsureAvailable(sizeof(uint));
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
                position += sizeof(uint);
                return value;
            }

            public void Skip(int byteCount)
            {
                EnsureAvailable(byteCount);
                position += byteCount;
            }

            public void AssertPosition(int expectedPosition, string layoutName)
            {
                if (position != expectedPosition)
                {
                    throw new InvalidDataException(
                        $"{layoutName} parser stopped at byte {position}; expected {expectedPosition}.");
                }
            }

            private void EnsureAvailable(int byteCount)
            {
                if (byteCount < 0 || position > data.Length - byteCount)
                {
                    throw new InvalidDataException(
                        $"Metrics table ended at byte {data.Length} while reading byte {position}.");
                }
            }
        }

        private sealed class MetricBuilder
        {
            private readonly List<MetricReading> items = new();

            public IReadOnlyList<MetricReading> Items => items;

            public void AddFrequency(string group, string name, ulong raw)
            {
                AddInteger(group, name, raw, "MHz");
            }

            public void AddPercent(string group, string name, ulong raw)
            {
                AddInteger(group, name, raw, "%");
            }

            public void AddInteger(string group, string name, ulong raw, string unit)
            {
                items.Add(new MetricReading(
                    group,
                    name,
                    raw.ToString(CultureInfo.InvariantCulture),
                    unit,
                    $"0x{raw:X}"));
            }

            public void AddScaled(
                string group,
                string name,
                double value,
                string unit,
                ulong raw,
                string format)
            {
                items.Add(new MetricReading(
                    group,
                    name,
                    value.ToString(format, CultureInfo.InvariantCulture),
                    unit,
                    $"0x{raw:X}"));
            }

            public void AddHex(string group, string name, ulong raw)
            {
                items.Add(new MetricReading(group, name, $"0x{raw:X8}", string.Empty, $"0x{raw:X}"));
            }

            public void AddFanPwm(byte raw)
            {
                AddScaled("Fan", "Fan PWM", raw * 100.0 / byte.MaxValue, "%", raw, "F1");
            }

            public void AddPcieRate(byte raw, bool isZeroBased)
            {
                int generation = isZeroBased ? raw + 1 : raw;
                string value = generation is >= 1 and <= 6
                    ? $"Gen {generation}"
                    : $"Encoding {raw}";
                items.Add(new MetricReading(
                    "PCI Express",
                    "Link rate",
                    value,
                    string.Empty,
                    $"0x{raw:X2}"));
            }

            public void AddPcieWidth(byte raw)
            {
                string value = raw is 1 or 2 or 4 or 8 or 12 or 16 or 32
                    ? $"x{raw}"
                    : $"Encoding {raw}";
                items.Add(new MetricReading("PCI Express", "Link width", value, string.Empty, $"0x{raw:X2}"));
            }

            public void AddSerial(uint lower, uint upper)
            {
                ulong serial = ((ulong)upper << 32) | lower;
                items.Add(new MetricReading(
                    "Device",
                    "Public serial number",
                    $"0x{serial:X16}",
                    string.Empty,
                    $"0x{upper:X8}{lower:X8}"));
            }
        }
    }
}
