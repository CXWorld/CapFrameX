using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameX.RadeonMonitor
{
    internal sealed class AdlPmLogClient : IDisposable
    {
        private const int AdlSuccess = 0;
        private const int MaximumAdapterCount = 256;
        private const int SensorCount = 256;

        private readonly object syncRoot = new();
        private readonly AdlNative.MemoryAllocate memoryAllocate;

        private IntPtr context;

        private AdlPmLogClient(
            IntPtr context,
            AdlNative.MemoryAllocate memoryAllocate,
            int adapterIndex,
            string adapterName)
        {
            this.context = context;
            this.memoryAllocate = memoryAllocate;
            AdapterIndex = adapterIndex;
            AdapterName = adapterName;
        }

        public int AdapterIndex { get; }

        public string AdapterName { get; }

        public static AdlPmLogClient Open(RadeonDeviceInfo deviceInfo)
        {
            ArgumentNullException.ThrowIfNull(deviceInfo);

            AdlNative.MemoryAllocate memoryAllocate = Marshal.AllocHGlobal;
            IntPtr context = IntPtr.Zero;
            try
            {
                ThrowIfFailed(
                    AdlNative.ADL2_Main_Control_Create(memoryAllocate, 1, ref context),
                    nameof(AdlNative.ADL2_Main_Control_Create));

                AdlNative.AdapterInfo adapter = FindAdapter(context, deviceInfo);
                return new AdlPmLogClient(
                    context,
                    memoryAllocate,
                    adapter.AdapterIndex,
                    string.IsNullOrWhiteSpace(adapter.AdapterName)
                        ? $"ADL adapter {adapter.AdapterIndex}"
                        : adapter.AdapterName);
            }
            catch
            {
                if (context != IntPtr.Zero)
                {
                    AdlNative.ADL2_Main_Control_Destroy(context);
                }

                GC.KeepAlive(memoryAllocate);
                throw;
            }
        }

        public AdlPmLogSnapshot ReadMetrics()
        {
            lock (syncRoot)
            {
                AdlNative.PmLogDataOutput output = QueryMetrics();

                List<MetricReading> readings = new();
                List<AdlPmLogValue> values = new();
                for (int sensorId = 1; sensorId < output.Sensors.Length; sensorId++)
                {
                    AdlNative.SingleSensorData sensor = output.Sensors[sensorId];
                    if (sensor.Supported == 0)
                    {
                        continue;
                    }

                    SensorDescriptor descriptor = GetSensorDescriptor(sensorId);
                    readings.Add(CreateReading(descriptor, sensor.Value));
                    values.Add(new AdlPmLogValue(sensorId, descriptor.Name, sensor.Value));
                }

                if (readings.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"ADL adapter {AdapterIndex} returned no supported PMLog sensors.");
                }

                return new AdlPmLogSnapshot(
                    AdapterIndex,
                    AdapterName,
                    readings,
                    FormatSensorDump(values));
            }
        }

        public void RefreshMetrics()
        {
            lock (syncRoot)
            {
                _ = QueryMetrics();
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (context == IntPtr.Zero)
                {
                    return;
                }

                AdlNative.ADL2_Main_Control_Destroy(context);
                context = IntPtr.Zero;
                GC.KeepAlive(memoryAllocate);
            }
        }

        private static AdlNative.AdapterInfo FindAdapter(IntPtr context, RadeonDeviceInfo deviceInfo)
        {
            int adapterCount = 0;
            ThrowIfFailed(
                AdlNative.ADL2_Adapter_NumberOfAdapters_Get(context, ref adapterCount),
                nameof(AdlNative.ADL2_Adapter_NumberOfAdapters_Get));

            if (adapterCount is <= 0 or > MaximumAdapterCount)
            {
                throw new InvalidOperationException($"ADL returned an invalid adapter count: {adapterCount}.");
            }

            int adapterInfoSize = Marshal.SizeOf<AdlNative.AdapterInfo>();
            int bufferSize = checked(adapterCount * adapterInfoSize);
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ThrowIfFailed(
                    AdlNative.ADL2_Adapter_AdapterInfo_Get(context, buffer, bufferSize),
                    nameof(AdlNative.ADL2_Adapter_AdapterInfo_Get));

                List<AdlNative.AdapterInfo> matches = new();
                for (int index = 0; index < adapterCount; index++)
                {
                    AdlNative.AdapterInfo adapter = Marshal.PtrToStructure<AdlNative.AdapterInfo>(
                        IntPtr.Add(buffer, checked(index * adapterInfoSize)));

                    if (adapter.BusNumber == deviceInfo.Bus &&
                        adapter.DeviceNumber == deviceInfo.Device &&
                        adapter.FunctionNumber == deviceInfo.Function)
                    {
                        matches.Add(adapter);
                    }
                }

                if (matches.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"ADL does not expose the PawnIO-selected GPU at PCI {deviceInfo.PciAddress}.");
                }

                return matches
                    .OrderByDescending(adapter => adapter.Present != 0 && adapter.Exist != 0)
                    .ThenBy(adapter => adapter.AdapterIndex)
                    .First();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private AdlNative.PmLogDataOutput QueryMetrics()
        {
            ObjectDisposedException.ThrowIf(context == IntPtr.Zero, this);

            AdlNative.PmLogDataOutput output = new()
            {
                Size = Marshal.SizeOf<AdlNative.PmLogDataOutput>(),
                Sensors = new AdlNative.SingleSensorData[SensorCount]
            };
            ThrowIfFailed(
                AdlNative.ADL2_New_QueryPMLogData_Get(context, AdapterIndex, ref output),
                nameof(AdlNative.ADL2_New_QueryPMLogData_Get));

            if (output.Sensors is null || output.Sensors.Length != SensorCount)
            {
                throw new InvalidOperationException(
                    $"ADL returned {output.Sensors?.Length ?? 0} PMLog sensor slots; expected {SensorCount}.");
            }

            return output;
        }

        private static MetricReading CreateReading(SensorDescriptor descriptor, int rawValue)
        {
            uint unsignedRaw = unchecked((uint)rawValue);
            string raw = $"0x{unsignedRaw:X}";
            string value = descriptor.Kind switch
            {
                SensorValueKind.Hex => $"0x{unsignedRaw:X8}",
                SensorValueKind.PcieGeneration when rawValue is >= 1 and <= 6 => $"Gen {rawValue}",
                SensorValueKind.PcieGeneration => $"Encoding {rawValue}",
                SensorValueKind.PcieWidth when rawValue is 1 or 2 or 4 or 8 or 12 or 16 or 32 => $"x{rawValue}",
                SensorValueKind.PcieWidth => $"Encoding {rawValue}",
                _ => rawValue.ToString(CultureInfo.InvariantCulture)
            };

            double? numericValue = descriptor.Kind == SensorValueKind.Hex ? null : rawValue;
            MetricValueKind valueKind = descriptor.Kind switch
            {
                SensorValueKind.PcieGeneration => MetricValueKind.PcieGeneration,
                SensorValueKind.PcieWidth => MetricValueKind.PcieWidth,
                _ => MetricValueKind.Numeric
            };

            return new MetricReading(
                descriptor.Group,
                descriptor.Name,
                value,
                descriptor.Unit,
                raw,
                numericValue,
                ValueKind: valueKind);
        }

        private static string FormatSensorDump(IReadOnlyList<AdlPmLogValue> values)
        {
            StringBuilder builder = new();
            builder.AppendLine("ID  ADL PMLog sensor                       Value       Raw");
            foreach (AdlPmLogValue value in values)
            {
                builder.Append(value.SensorId.ToString("D3", CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append(value.Name.PadRight(38));
                builder.Append(' ');
                builder.Append(value.Value.ToString(CultureInfo.InvariantCulture).PadLeft(10));
                builder.Append("  0x");
                builder.Append(unchecked((uint)value.Value).ToString("X8", CultureInfo.InvariantCulture));
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static SensorDescriptor GetSensorDescriptor(int sensorId)
        {
            return sensorId switch
            {
                1 => new("Clocks", "GFX clock", "MHz"),
                2 => new("Clocks", "Memory clock", "MHz"),
                3 => new("Clocks", "SOC clock", "MHz"),
                4 => new("Video", "UVD clock 1", "MHz"),
                5 => new("Video", "UVD clock 2", "MHz"),
                6 => new("Video", "VCE clock", "MHz"),
                7 => new("Video", "VCN clock", "MHz"),
                8 => new("Temperature", "Edge", "°C"),
                9 => new("Temperature", "Memory", "°C"),
                10 => new("Temperature", "VR VDDC", "°C"),
                11 => new("Temperature", "VR memory", "°C"),
                12 => new("Temperature", "Liquid", "°C"),
                13 => new("Temperature", "PLX", "°C"),
                14 => new("Fan", "Fan speed", "RPM"),
                15 => new("Fan", "Fan duty", "%"),
                16 => new("Voltage", "SOC voltage", "mV"),
                17 => new("Power", "SOC power", "W"),
                18 => new("Current", "SOC current", "A"),
                19 => new("Activity", "GFX activity", "%"),
                20 => new("Activity", "Memory activity", "%"),
                21 => new("Voltage", "GFX voltage", "mV"),
                22 => new("Voltage", "Memory voltage", "mV"),
                23 => new("Power", "ASIC power", "W"),
                24 => new("Temperature", "VR SOC", "°C"),
                25 => new("Temperature", "VR memory 0", "°C"),
                26 => new("Temperature", "VR memory 1", "°C"),
                27 => new("Temperature", "Hotspot", "°C"),
                28 => new("Temperature", "GFX", "°C"),
                29 => new("Temperature", "SOC", "°C"),
                30 => new("Power", "GFX power", "W"),
                31 => new("Current", "GFX current", "A"),
                32 => new("Temperature", "CPU", "°C"),
                33 => new("Power", "CPU power", "W"),
                34 => new("Clocks", "CPU clock", "MHz"),
                35 => new("Throttling", "Throttler status", string.Empty, SensorValueKind.Hex),
                36 => new("Video", "VCN1 clock 1", "MHz"),
                37 => new("Video", "VCN1 clock 2", "MHz"),
                38 => new("SmartShift", "CPU power shift", "%"),
                39 => new("SmartShift", "dGPU power shift", "%"),
                40 => new("PCI Express", "Link rate", string.Empty, SensorValueKind.PcieGeneration),
                41 => new("PCI Express", "Link width", string.Empty, SensorValueKind.PcieWidth),
                42 => new("Temperature", "Liquid 0", "°C"),
                43 => new("Temperature", "Liquid 1", "°C"),
                44 => new("Clocks", "Fabric clock", "MHz"),
                45 => new("Throttling", "CPU throttler status", string.Empty, SensorValueKind.Hex),
                46 => new("Power", "SmartShift paired ASIC power", "W"),
                47 => new("Power", "SmartShift total power limit", "W"),
                48 => new("Power", "SmartShift APU power limit", "W"),
                49 => new("Power", "SmartShift dGPU power limit", "W"),
                50 => new("Temperature", "Hotspot GCD", "°C"),
                51 => new("Temperature", "Hotspot MCD", "°C"),
                52 => new("Throttling", "Temperature edge", "%"),
                53 => new("Throttling", "Temperature hotspot", "%"),
                54 => new("Throttling", "Temperature hotspot GCD", "%"),
                55 => new("Throttling", "Temperature hotspot MCD", "%"),
                56 => new("Throttling", "Temperature memory", "%"),
                57 => new("Throttling", "Temperature VR GFX", "%"),
                58 => new("Throttling", "Temperature VR memory 0", "%"),
                59 => new("Throttling", "Temperature VR memory 1", "%"),
                60 => new("Throttling", "Temperature VR SOC", "%"),
                61 => new("Throttling", "Temperature liquid 0", "%"),
                62 => new("Throttling", "Temperature liquid 1", "%"),
                63 => new("Throttling", "Temperature PLX", "%"),
                64 => new("Throttling", "TDC GFX", "%"),
                65 => new("Throttling", "TDC SOC", "%"),
                66 => new("Throttling", "TDC user", "%"),
                67 => new("Throttling", "PPT0", "%"),
                68 => new("Throttling", "PPT1", "%"),
                69 => new("Throttling", "PPT2", "%"),
                70 => new("Throttling", "PPT3", "%"),
                71 => new("Throttling", "FIT", "%"),
                72 => new("Throttling", "GFX APCC+", "%"),
                73 => new("Power", "Board power", "W"),
                _ => new("ADL PMLog", $"Sensor {sensorId}", "raw")
            };
        }

        private static void ThrowIfFailed(int status, string operation)
        {
            if (status != AdlSuccess)
            {
                throw new InvalidOperationException($"{operation} failed with ADL status {status}.");
            }
        }

        private enum SensorValueKind
        {
            Integer,
            Hex,
            PcieGeneration,
            PcieWidth
        }

        private readonly record struct SensorDescriptor(
            string Group,
            string Name,
            string Unit,
            SensorValueKind Kind = SensorValueKind.Integer);

        private sealed record AdlPmLogValue(int SensorId, string Name, int Value);

        private static class AdlNative
        {
            private const string LibraryName = "atiadlxx.dll";

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate IntPtr MemoryAllocate(int size);

            [StructLayout(LayoutKind.Sequential)]
            internal struct SingleSensorData
            {
                public int Supported;
                public int Value;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct PmLogDataOutput
            {
                public int Size;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = SensorCount)]
                public SingleSensorData[] Sensors;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            internal struct AdapterInfo
            {
                public int Size;
                public int AdapterIndex;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string? Udid;

                public int BusNumber;
                public int DeviceNumber;
                public int FunctionNumber;
                public int VendorId;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string? AdapterName;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string? DisplayName;

                public int Present;
                public int Exist;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string? DriverPath;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string? DriverPathExt;

                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
                public string? PnpString;

                public int OsDisplayIndex;
            }

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern int ADL2_Main_Control_Create(
                MemoryAllocate callback,
                int connectedAdapters,
                ref IntPtr context);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern int ADL2_Main_Control_Destroy(IntPtr context);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern int ADL2_Adapter_NumberOfAdapters_Get(
                IntPtr context,
                ref int adapterCount);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern int ADL2_Adapter_AdapterInfo_Get(
                IntPtr context,
                IntPtr adapterInfo,
                int inputSize);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern int ADL2_New_QueryPMLogData_Get(
                IntPtr context,
                int adapterIndex,
                ref PmLogDataOutput output);
        }
    }

    internal sealed record AdlPmLogSnapshot(
        int AdapterIndex,
        string AdapterName,
        IReadOnlyList<MetricReading> Readings,
        string SensorDump);
}
