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
            reader.Skip(6 * sizeof(ushort));

            metrics.AddPercent("Activity", "GFX activity", reader.ReadUInt16());
            metrics.AddPercent("Activity", "UCLK activity", reader.ReadUInt16());

            AddRdna2Voltage(metrics, "SOC voltage", reader.ReadByte());
            AddRdna2Voltage(metrics, "GFX voltage", reader.ReadByte());
            AddRdna2Voltage(metrics, "Memory VID", reader.ReadByte());
            reader.Skip(1);

            metrics.AddInteger("Power", "Socket power", reader.ReadUInt16(), "W");

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
            reader.Skip(4 * sizeof(ushort));

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
            reader.Skip(sizeof(ushort));

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
                    metrics.AddInteger("SmartShift", "APU socket power", reader.ReadByte(), "W");
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
            SkipCommonRdna3And4Averages(reader, metrics);
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
            metrics.AddInteger("Power", "Socket power", reader.ReadUInt16(), "W");
            metrics.AddInteger("Power", "Total board power", reader.ReadUInt16(), "W");

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
            metrics.AddInteger("SmartShift", "APU socket power", reader.ReadUInt16(), "W");
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
            MetricBuilder clocks = new();

            uint[] currentClocks = ReadClockValues(reader, Rdna4ClockNames.Length);
            CommonClockValues clockValues = ReadCommonClockValues(reader, metrics);
            reader.Skip(16 * sizeof(ushort));

            metrics.AddInteger("Counters", "Metrics counter", reader.ReadUInt32(), string.Empty);
            ReadVoltageAndCurrentPlanes(
                reader,
                metrics,
                new[] { "GFX", "SOC", "VDDCI memory", "VDDIO memory" });

            ushort gfxActivity = reader.ReadUInt16();
            ushort uclkActivity = reader.ReadUInt16();
            AddRdna4Clocks(clocks, currentClocks, clockValues, gfxActivity, uclkActivity);
            metrics.AddPercent("Activity", "GFX activity", gfxActivity);
            metrics.AddPercent("Activity", "UCLK activity", uclkActivity);
            metrics.AddPercent("Video", "VCN 0 activity", reader.ReadUInt16());
            metrics.AddPercent("Video", "VCN 1 activity", reader.ReadUInt16());
            metrics.AddInteger("Counters", "Energy accumulator", reader.ReadUInt32(), "raw");
            metrics.AddInteger("Power", "Socket power", reader.ReadUInt16(), "W");
            metrics.AddInteger("Power", "Total board power", reader.ReadUInt16(), "W");

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
            metrics.AddInteger("SmartShift", "APU socket power", reader.ReadUInt16(), "W");
            metrics.AddPercent("Activity", "Maximum UCLK activity", reader.ReadUInt16());
            metrics.AddSerial(reader.ReadUInt32(), reader.ReadUInt32());

            reader.AssertPosition(260, "RDNA4");
            return clocks.Items.Concat(metrics.Items).ToArray();
        }

        private static uint[] ReadClockValues(MetricsReader reader, int count)
        {
            uint[] values = new uint[count];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = reader.ReadUInt32();
            }

            return values;
        }

        private static CommonClockValues ReadCommonClockValues(
            MetricsReader reader,
            MetricBuilder metrics)
        {
            CommonClockValues values = new(
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16(),
                reader.ReadUInt16());
            reader.Skip(sizeof(ushort));
            metrics.AddInteger("Power", "dGPU W_MAX", reader.ReadUInt16(), "W");
            reader.Skip(sizeof(ushort));
            return values;
        }

        private static void AddRdna4Clocks(
            MetricBuilder metrics,
            IReadOnlyList<uint> current,
            CommonClockValues values,
            ushort gfxActivity,
            ushort uclkActivity)
        {
            const ushort busyThreshold = 5;
            uint[] selected = current.ToArray();
            selected[0] = gfxActivity <= busyThreshold
                ? values.GfxAfterDeepSleep
                : values.GfxBeforeDeepSleep;
            selected[2] = uclkActivity <= busyThreshold
                ? values.UclkAfterDeepSleep
                : values.UclkBeforeDeepSleep;
            selected[4] = values.Dclk0;
            selected[5] = values.Vclk0;

            for (int index = 0; index < selected.Length; index++)
            {
                metrics.AddFrequency("Current clocks", Rdna4ClockNames[index], selected[index]);
            }
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

        private static void SkipCommonRdna3And4Averages(
            MetricsReader reader,
            MetricBuilder metrics)
        {
            // Eleven average clocks plus PCIe busy.
            reader.Skip(12 * sizeof(ushort));
            metrics.AddInteger("Power", "dGPU W_MAX", reader.ReadUInt16(), "W");
            reader.Skip(2);
        }

        private readonly record struct CommonClockValues(
            ushort GfxTarget,
            ushort GfxBeforeDeepSleep,
            ushort GfxAfterDeepSleep,
            ushort FclkBeforeDeepSleep,
            ushort FclkAfterDeepSleep,
            ushort UclkBeforeDeepSleep,
            ushort UclkAfterDeepSleep,
            ushort Vclk0,
            ushort Dclk0,
            ushort Vclk1,
            ushort Dclk1);

        private static void ReadVoltageAndCurrentPlanes(
            MetricsReader reader,
            MetricBuilder metrics,
            IReadOnlyList<string> planeNames)
        {
            foreach (string plane in planeNames)
            {
                metrics.AddInteger("Voltage", $"{plane} voltage", reader.ReadUInt16(), "mV");
            }

            foreach (string plane in planeNames)
            {
                ushort raw = reader.ReadUInt16();
                metrics.AddScaled(
                    "Current",
                    $"{plane} current",
                    raw / 1000.0,
                    "A",
                    raw,
                    decimalPlaces: 3);
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
            metrics.AddScaled("Voltage", name, millivolts, "mV", raw, decimalPlaces: 2);
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
                    $"0x{raw:X}",
                    (double)raw));
            }

            public void AddScaled(
                string group,
                string name,
                double value,
                string unit,
                ulong raw,
                int decimalPlaces)
            {
                items.Add(new MetricReading(
                    group,
                    name,
                    value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture),
                    unit,
                    $"0x{raw:X}",
                    value,
                    decimalPlaces));
            }

            public void AddHex(string group, string name, ulong raw)
            {
                items.Add(new MetricReading(group, name, $"0x{raw:X8}", string.Empty, $"0x{raw:X}"));
            }

            public void AddFanPwm(byte raw)
            {
                // PMFW reports percent; hwmon uses a 0..255 scale.
                AddScaled("Fan", "Fan PWM", raw, "%", raw, decimalPlaces: 1);
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
                    $"0x{raw:X2}",
                    generation,
                    ValueKind: MetricValueKind.PcieGeneration));
            }

            public void AddPcieWidth(byte raw)
            {
                string value = raw is 1 or 2 or 4 or 8 or 12 or 16 or 32
                    ? $"x{raw}"
                    : $"Encoding {raw}";
                items.Add(new MetricReading(
                    "PCI Express",
                    "Link width",
                    value,
                    string.Empty,
                    $"0x{raw:X2}",
                    raw,
                    ValueKind: MetricValueKind.PcieWidth));
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
