using System;

namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// Provides typed access to the Intel client IMC clock-ratio module
/// (<c>IntelIMC.bin</c>) running inside the PawnIO driver.
/// </summary>
/// <remarks>
/// The kernel module exposes two production IOCTLs and decodes a single
/// 7-cell output buffer; this wrapper does not interpret the ratio into MHz
/// (BCLK measurement lives at the call site) but does expose the
/// reference-clock mapping the module publishes via <see cref="GetReferenceMHz"/>.
/// </remarks>
public class IntelImc
{
    /// <summary>Identifies which on-die register populated the ratio.</summary>
    public enum ImcSource
    {
        /// <inheritdoc />
        None = 0,
        /// <inheritdoc />
        MchbarMemssPma = 1,
        /// <inheritdoc />
        MchbarSaPerf = 2,
        /// <inheritdoc />
        PmtQclkStatus = 3,
        /// <inheritdoc />
        MchbarLiveGvArl = 4,
    }

    /// <summary>What the ratio is multiplied with on this hardware.</summary>
    public enum ImcReferenceClock
    {
        /// <inheritdoc />
        Unknown = 0,
        /// <inheritdoc />
        BclkDiv3 = 1,
        /// <inheritdoc />
        Bclk = 2,
        /// <inheritdoc />
        BclkMul4Div3 = 3,
    }

    /// <summary>
    /// DDR controller "gear". Numeric value is the data-rate multiplier
    /// against QCLK (1, 2, 4); <see cref="Unknown"/> means the source register
    /// does not encode a gear field.
    /// </summary>
    public enum ImcGear
    {/// <inheritdoc />
        Unknown = 0,
        /// <inheritdoc />
        Gear1 = 1,
        /// <inheritdoc />
        Gear2 = 2,
        /// <inheritdoc />
        Gear4 = 4,
    }

    /// <summary>Hints about how the ratio should be interpreted.</summary>
    [Flags]
    public enum ImcClockFlags
    {
        /// <inheritdoc />
        None = 0,
        /// <inheritdoc />
        StaticLocked = 1 << 0,
        /// <inheritdoc />
        LiveCurrent = 1 << 1,
        /// <inheritdoc />
        Experimental = 1 << 2,
    }

    /// <summary>Decoded result of one IOCTL call.</summary>
    public readonly struct ImcClock
    {
        /// <summary>
        /// Creates a new <see cref="ImcClock"/> with the specified properties.
        /// </summary>
        /// <param name="abiVersion"></param>
        /// <param name="source"></param>
        /// <param name="ratio"></param>
        /// <param name="referenceClock"></param>
        /// <param name="gear"></param>
        /// <param name="rawRegister"></param>
        /// <param name="flags"></param>
        public ImcClock(uint abiVersion, ImcSource source, uint ratio, ImcReferenceClock referenceClock, ImcGear gear, uint rawRegister, ImcClockFlags flags)
        {
            AbiVersion = abiVersion;
            Source = source;
            Ratio = ratio;
            ReferenceClock = referenceClock;
            Gear = gear;
            RawRegister = rawRegister;
            Flags = flags;
        }

        /// <summary>
        /// Version of the ABI the driver implements. Consumers can use this to
        /// </summary>
        public uint AbiVersion { get; }
        /// <summary>
        /// Identifies which on-die register populated the ratio. Consumers can use
        /// </summary>
        public ImcSource Source { get; }
        /// <summary>
        /// The raw ratio value read from the register, before applying the reference 
        /// </summary>
        public uint Ratio { get; }
        /// <summary>
        /// What the ratio is multiplied with on this hardware. Consumers can use this
        /// </summary>
        public ImcReferenceClock ReferenceClock { get; }
        /// <summary>
        /// DDR controller "gear". Numeric value is the data-rate multiplier against QCLK (1, 2, 4);
        /// </summary>
        public ImcGear Gear { get; }
        /// <summary>
        /// The raw 32-bit value read from the source register. This is useful for consumers who want to apply their own decoding logic or validate the driver's interpretation.
        /// </summary>
        public uint RawRegister { get; }
        /// <summary>
        /// Hints about how the ratio should be interpreted. For example, <see cref="ImcClockFlags.StaticLocked"/> indicates the ratio is a static maximum value, while <see cref="ImcClockFlags.LiveCurrent"/> indicates it's a live current value. Consumers can use these flags to determine how to interpret the ratio and whether it reflects current conditions or static limits.
        /// </summary>
        public ImcClockFlags Flags { get; }
    }

    private const string IoctlReadImcClock = "ioctl_read_imc_clock";
    private const string IoctlReadImcClockLive = "ioctl_read_imc_clock_live";
    private const int OutCellCount = 7;

    private static readonly long[] _emptyInput = Array.Empty<long>();
    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(IntelImc).Assembly, $"{nameof(LibreHardwareMonitor)}.Resources.PawnIO.IntelIMC.bin");

    /// <summary>
    /// Reads the static (locked-max on Core Ultra, live on Alder/Raptor Lake)
    /// IMC clock ratio. Allowlist: ADL/RPL/MTL/ARL/LNL/PTL plus Nova Lake
    /// (experimental). Returns <c>false</c> if the running CPU is outside
    /// the allowlist or any consistency check fails inside the driver.
    /// </summary>
    public bool ReadClock(out ImcClock clock)
    {
        return TryExecute(IoctlReadImcClock, out clock);
    }

    /// <summary>
    /// Reads the live IMC workpoint on Core Ultra: SA_PERF_STATUS @ 0x5918
    /// for MTL/LNL/PTL (and NVL, experimental), IMC_LIVE_GV_STATUS_ARL @
    /// 0xE448 for ARL. Returns <c>false</c> on ADL/RPL (use the static IOCTL
    /// — already live there) and on unknown CPUs.
    /// </summary>
    public bool ReadLiveClock(out ImcClock clock)
    {
        return TryExecute(IoctlReadImcClockLive, out clock);
    }

    /// <summary>
    /// Converts the module's reference-clock enum into MHz using the BCLK
    /// the consumer has measured. Mirrors the IMC_REF_* table in IntelIMC.p.
    /// Returns <c>0</c> for <see cref="ImcReferenceClock.Unknown"/>.
    /// </summary>
    public static double GetReferenceMHz(ImcReferenceClock reference, double bclkMHz)
    {
        return reference switch
        {
            ImcReferenceClock.BclkDiv3 => bclkMHz / 3.0,
            ImcReferenceClock.Bclk => bclkMHz,
            ImcReferenceClock.BclkMul4Div3 => bclkMHz * 4.0 / 3.0,
            _ => 0.0,
        };
    }

    /// <summary>Closes the underlying PawnIO module handle.</summary>
    public void Close() => _pawnIO.Close();

    private bool TryExecute(string ioctl, out ImcClock clock)
    {
        clock = default;
        try
        {
            long[] outArray = _pawnIO.Execute(ioctl, _emptyInput, OutCellCount);
            if (outArray == null || outArray.Length < OutCellCount)
                return false;

            clock = Decode(outArray);
            return clock.Source != ImcSource.None;
        }
        catch
        {
            return false;
        }
    }

    private static ImcClock Decode(long[] o)
    {
        return new ImcClock(
            abiVersion: (uint)o[0],
            source: (ImcSource)(int)o[1],
            ratio: (uint)o[2],
            referenceClock: (ImcReferenceClock)(int)o[3],
            gear: (ImcGear)(int)o[4],
            rawRegister: (uint)o[5],
            flags: (ImcClockFlags)(int)o[6]);
    }
}
