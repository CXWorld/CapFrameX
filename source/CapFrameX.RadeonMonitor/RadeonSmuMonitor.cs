using System.IO;

namespace CapFrameX.RadeonMonitor
{
    internal sealed class RadeonSmuMonitor : IDisposable
    {
        public const ulong RequiredModuleAbi = 5;

        private const int DeviceInfoEntryCount = 21;
        private const int Rdna2DwordCount = 41;
        private const int Smu13_0_0DwordCount = 61;
        private const int Smu13_0_7DwordCount = 60;
        private const int Rdna4DwordCount = 65;
        private const int Navi21SviDwordCount = 4;
        private const int RdnaToolTableQwordCount = 0x2000 / sizeof(ulong);
        private const int RdnaToolMetadataQwordCount = 4;
        private const int RdnaToolOutputQwordCount =
            RdnaToolMetadataQwordCount + RdnaToolTableQwordCount;
        private const int HResultDeviceNotReady = unchecked((int)0x80070015);
        private const int HResultAccessDenied = unchecked((int)0x80070005);

        private readonly PawnIoClient client;

        public RadeonSmuMonitor(PawnIoClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public RadeonDeviceInfo GetDeviceInfo()
        {
            ulong[] values = PciBusSynchronization.Execute(() =>
                client.Execute("ioctl_get_device_info", DeviceInfoEntryCount));
            RadeonDeviceInfo info = new(
                ModuleAbi: values[0],
                Bus: checked((byte)values[1]),
                Device: checked((byte)values[2]),
                Function: checked((byte)values[3]),
                DeviceId: checked((ushort)values[4]),
                RevisionId: checked((byte)values[5]),
                SubsystemVendorId: checked((ushort)values[6]),
                SubsystemDeviceId: checked((ushort)values[7]),
                RegisterBar: values[8],
                RegisterBarSize: values[9],
                VramBar: values[10],
                VramBarSize: values[11],
                MetricsGpuAddress: values[12],
                MetricsVramOffset: values[13],
                MetricsPhysicalAddress: values[14],
                Rdna2DwordCount: checked((int)values[15]),
                Smu13_0_0DwordCount: checked((int)values[16]),
                Smu13_0_7DwordCount: checked((int)values[17]),
                Rdna4DwordCount: checked((int)values[18]),
                Navi21SviDwordCount: checked((int)values[19]),
                RdnaToolTableQwordCount: checked((int)values[20]),
                PawnIoLibraryVersion: client.LibraryVersion);

            if (info.ModuleAbi < RequiredModuleAbi)
            {
                throw new InvalidDataException(
                    $"RadeonSMU module ABI {info.ModuleAbi} is too old; ABI {RequiredModuleAbi} is required.");
            }

            if (info.Rdna2DwordCount != Rdna2DwordCount ||
                info.Smu13_0_0DwordCount != Smu13_0_0DwordCount ||
                info.Smu13_0_7DwordCount != Smu13_0_7DwordCount ||
                info.Rdna4DwordCount != Rdna4DwordCount ||
                info.Navi21SviDwordCount != Navi21SviDwordCount ||
                info.RdnaToolTableQwordCount != RdnaToolTableQwordCount)
            {
                throw new InvalidDataException(
                    "The RadeonSMU metrics sizes do not match this test application's ABI.");
            }

            return info;
        }

        public uint[] ReadMetrics(RadeonGeneration generation, Rdna3MetricsLayout rdna3Layout)
        {
            PawnIoExecutionResult execution = ExecuteMetricsRead(generation, rdna3Layout);
            PawnIoClient.ThrowIfFailed(execution.HResult, $"execute {execution.FunctionName}");
            return ConvertToDwords(execution.Output);
        }

        public uint[]? TryReadMetrics(RadeonGeneration generation, Rdna3MetricsLayout rdna3Layout)
        {
            PawnIoExecutionResult execution = ExecuteMetricsRead(generation, rdna3Layout);
            if (!execution.Succeeded && IsRawMetricsAddressUnavailable(execution.HResult))
            {
                return null;
            }

            PawnIoClient.ThrowIfFailed(execution.HResult, $"execute {execution.FunctionName}");
            return ConvertToDwords(execution.Output);
        }

        private PawnIoExecutionResult ExecuteMetricsRead(
            RadeonGeneration generation,
            Rdna3MetricsLayout rdna3Layout)
        {
            (string functionName, int dwordCount) = generation switch
            {
                RadeonGeneration.Rdna2 => ("ioctl_read_metrics_rdna2", Rdna2DwordCount),
                RadeonGeneration.Rdna3 when rdna3Layout == Rdna3MetricsLayout.Smu13_0_0 =>
                    ("ioctl_read_metrics_rdna3_0", Smu13_0_0DwordCount),
                RadeonGeneration.Rdna3 when rdna3Layout == Rdna3MetricsLayout.Smu13_0_7 =>
                    ("ioctl_read_metrics_rdna3_7", Smu13_0_7DwordCount),
                RadeonGeneration.Rdna3 => throw new ArgumentException(
                    "The RDNA3 metrics layout must be resolved before reading.",
                    nameof(rdna3Layout)),
                RadeonGeneration.Rdna4 => ("ioctl_read_metrics_rdna4", Rdna4DwordCount),
                _ => throw new ArgumentOutOfRangeException(nameof(generation))
            };

            return PciBusSynchronization.Execute(() =>
                client.ExecuteWithStatus(functionName, dwordCount));
        }

        private static uint[] ConvertToDwords(ulong[] values)
        {
            uint[] result = new uint[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = checked((uint)values[i]);
            }

            return result;
        }

        public uint[] ReadNavi21SviTelemetry()
        {
            ulong[] values = PciBusSynchronization.Execute(() =>
                client.Execute("ioctl_read_navi21_svi", Navi21SviDwordCount));
            uint[] result = new uint[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = checked((uint)values[i]);
            }

            return result;
        }

        private static bool IsRawMetricsAddressUnavailable(int hResult)
        {
            return hResult is HResultDeviceNotReady or HResultAccessDenied;
        }

        public RadeonToolTableSnapshot ReadToolTable()
        {
            ulong[] values = PciBusSynchronization.Execute(() =>
                client.Execute(
                    "ioctl_read_rdna_tool_table",
                    RdnaToolOutputQwordCount));

            uint[] dwords = new uint[RdnaToolTableQwordCount * 2];
            for (int i = 0; i < RdnaToolTableQwordCount; i++)
            {
                ulong value = values[RdnaToolMetadataQwordCount + i];
                dwords[i * 2] = (uint)value;
                dwords[i * 2 + 1] = (uint)(value >> 32);
            }

            return new RadeonToolTableSnapshot(
                Version: checked((uint)values[0]),
                GpuAddress: values[1],
                FramebufferBase: values[2],
                FramebufferTop: values[3],
                Dwords: dwords);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }

    internal sealed record RadeonDeviceInfo(
        ulong ModuleAbi,
        byte Bus,
        byte Device,
        byte Function,
        ushort DeviceId,
        byte RevisionId,
        ushort SubsystemVendorId,
        ushort SubsystemDeviceId,
        ulong RegisterBar,
        ulong RegisterBarSize,
        ulong VramBar,
        ulong VramBarSize,
        ulong MetricsGpuAddress,
        ulong MetricsVramOffset,
        ulong MetricsPhysicalAddress,
        int Rdna2DwordCount,
        int Smu13_0_0DwordCount,
        int Smu13_0_7DwordCount,
        int Rdna4DwordCount,
        int Navi21SviDwordCount,
        int RdnaToolTableQwordCount,
        uint PawnIoLibraryVersion)
    {
        public string PciAddress => $"{Bus:X2}:{Device:X2}.{Function}";

        public string PawnIoVersion =>
            $"{PawnIoLibraryVersion >> 16}.{(PawnIoLibraryVersion >> 8) & 0xFF}.{PawnIoLibraryVersion & 0xFF}";
    }

    internal sealed record RadeonToolTableSnapshot(
        uint Version,
        ulong GpuAddress,
        ulong FramebufferBase,
        ulong FramebufferTop,
        uint[] Dwords)
    {
        public int Layout => ((Version >> 16) & 0xFFFF) switch
        {
            0x0000 => 1,
            0x0027 => 2,
            0x0028 => 3,
            0x0029 => 4,
            0x0034 => 5,
            0x003A => 6,
            0x004E => 7,
            0x0066 => 8,
            0x0044 => 9,
            0x0055 => 10,
            0x0056 => 11,
            _ => 0
        };
    }
}
