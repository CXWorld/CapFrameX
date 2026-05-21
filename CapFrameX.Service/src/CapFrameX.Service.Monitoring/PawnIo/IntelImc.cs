using CapFrameX.Service.Monitoring.Hardware;
using System;

namespace CapFrameX.Service.Monitoring.PawnIo;

/// <summary>
/// Provides typed access to Intel client IMC clock-ratio data, decoded
/// against MCHBAR registers exposed by the generic <c>IntelMCHBAR.bin</c>
/// PawnIO module.
/// </summary>
/// <remarks>
/// All platform dispatch (CPUID allowlist, register selection, reserved-bits
/// and ratio-range checks, ARL live-clamp, gear extraction) lives on the
/// C# side. The kernel module only resolves MCHBAR and reads dwords from it.
/// Consumers that previously talked to <c>IntelIMC.bin</c> see the same
/// public API and the same <see cref="ImcClock"/> output buffer semantics.
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
    {
        /// <inheritdoc />
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
        /// Version of the ABI the wrapper implements.
        /// </summary>
        public uint AbiVersion { get; }
        /// <summary>
        /// Identifies which on-die register populated the ratio.
        /// </summary>
        public ImcSource Source { get; }
        /// <summary>
        /// The raw ratio value read from the register, before applying the reference clock.
        /// </summary>
        public uint Ratio { get; }
        /// <summary>
        /// What the ratio is multiplied with on this hardware.
        /// </summary>
        public ImcReferenceClock ReferenceClock { get; }
        /// <summary>
        /// DDR controller "gear". Numeric value is the data-rate multiplier against QCLK (1, 2, 4).
        /// </summary>
        public ImcGear Gear { get; }
        /// <summary>
        /// The raw 32-bit value read from the source register.
        /// </summary>
        public uint RawRegister { get; }
        /// <summary>
        /// Hints about how the ratio should be interpreted.
        /// </summary>
        public ImcClockFlags Flags { get; }
    }

    private enum Platform
    {
        None = 0,
        Mtl,
        Arl,
        Lnl,
        Ptl,
        Adl,
        Rpl,
        Nvl,
    }

    private const uint AbiVersion = 1;

    private const int MEMSS_PMA_CR_BIOS_DATA = 0x13D10;
    private const int SA_PERF_STATUS = 0x5918;
    private const int IMC_LIVE_GV_STATUS_ARL = 0xE448;

    // ADL/RPL: MC_BIOS_DATA mirror of the BIOS-programmed memory controller
    // configuration. Not in Intel's public client CFG/MEM datasheet. Layout
    // empirically validated on i9-13900HX with DDR5-5600 in Gear2 (Memory
    // Data Rate matched 5600 MT/s after applying):
    //   bits 11:8   gear-down stage      (0=Gear1, 1=Gear2, 2=Gear4)
    // The gear maps as 2^stage; anything beyond stage 2 is treated as
    // "wrong register / future stage" and falls back to ImcGear.Unknown.
    private const int MC_BIOS_DATA_ADL_RPL = 0x5E04;

    private const uint IMC_RATIO_MIN = 16;
    private const uint IMC_RATIO_MAX = 220;

    private const uint MEMSS_PMA_RESERVED_MASK_STRICT = 0xFFFFFE00u;
    private const uint MEMSS_PMA_RESERVED_MASK_NONE = 0u;

    private const string IoctlReadDword = "ioctl_read_dword";

    private readonly long[] _readDwordIn = new long[1];
    private readonly long[] _readDwordOut = new long[1];

    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(IntelImc).Assembly, "CapFrameX.Service.Monitoring.Resources.PawnIO.IntelMCHBAR.bin");

    private readonly Platform _platform;
    private readonly ImcSource _source;
    private readonly bool _validated;
    private readonly uint _pmaReservedMask;

    /// <summary>
    /// Creates a new <see cref="IntelImc"/>. Loads the IntelMCHBAR PawnIO
    /// module and resolves the running CPU to a register-layout family;
    /// instances on unsupported CPUs leave <see cref="ReadClock"/> /
    /// <see cref="ReadLiveClock"/> returning <c>false</c>.
    /// </summary>
    public IntelImc()
    {
        _platform = ResolvePlatform();
        _source = GetPlatformSource(_platform);
        _validated = IsPlatformValidated(_platform);
        _pmaReservedMask = GetPlatformPmaReservedMask(_platform);
    }

    /// <summary>
    /// Reads the static (locked-max on Core Ultra, live on Alder/Raptor Lake)
    /// IMC clock ratio. Allowlist: ADL/RPL/MTL/ARL/LNL/PTL plus Nova Lake
    /// (experimental). Returns <c>false</c> if the running CPU is outside
    /// the allowlist, MCHBAR is firmware-disabled, or any consistency check
    /// fails.
    /// </summary>
    public bool ReadClock(out ImcClock clock)
    {
        clock = default;
        if (_source == ImcSource.None)
            return false;

        ImcClockFlags baseFlags = _validated ? ImcClockFlags.None : ImcClockFlags.Experimental;

        switch (_source)
        {
            case ImcSource.MchbarMemssPma:
                {
                    if (!ReadMemssPma(out uint raw, out uint ratio, out ImcGear gear))
                        return false;
                    clock = new ImcClock(
                        AbiVersion,
                        ImcSource.MchbarMemssPma,
                        ratio,
                        ImcReferenceClock.BclkDiv3,
                        gear,
                        raw,
                        baseFlags | ImcClockFlags.StaticLocked);
                    return true;
                }
            case ImcSource.MchbarSaPerf:
                {
                    if (!ReadSaPerfStatus(out uint raw, out uint ratio, out ImcReferenceClock refMode))
                        return false;
                    // SA_PERF_STATUS does not encode gear on ADL/RPL. Probe
                    // MC_BIOS_DATA as a best-effort source so consumers can
                    // compute Memory Data Rate and DRAM Frequency.
                    TryReadAdlRplGear(out ImcGear gear);
                    clock = new ImcClock(
                        AbiVersion,
                        ImcSource.MchbarSaPerf,
                        ratio,
                        refMode,
                        gear,
                        raw,
                        baseFlags | ImcClockFlags.LiveCurrent);
                    return true;
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Reads the live IMC workpoint on Core Ultra: SA_PERF_STATUS @ 0x5918
    /// for MTL/LNL/PTL (and NVL, experimental), IMC_LIVE_GV_STATUS_ARL @
    /// 0xE448 for ARL. Returns <c>false</c> on ADL/RPL (use the static IOCTL
    /// — already live there) and on unknown CPUs.
    /// </summary>
    public bool ReadLiveClock(out ImcClock clock)
    {
        clock = default;
        if (_source != ImcSource.MchbarMemssPma)
            return false;

        // Pull gear + trained_max from MEMSS_PMA first; trained_max feeds the
        // ARL clamp. On reserved-bits / range failure we fall back to Unknown
        // gear and skip the clamp rather than failing the IOCTL.
        ImcGear gear = ImcGear.Unknown;
        uint trainedMax = 0;
        if (ReadMemssPma(out _, out uint pmaRatio, out ImcGear pmaGear))
        {
            gear = pmaGear;
            trainedMax = pmaRatio;
        }

        uint raw;
        uint ratio;
        ImcSource liveSource;
        if (_platform == Platform.Arl)
        {
            if (!ReadArlLiveGvStatus(out raw, out ratio))
                return false;
            // ARL clamp: high state encodes trained_max + 1 (PLL overshoot);
            // clamp to MEMSS_PMA so output matches HWiNFO/CPU-Z. Skip if
            // trained_max == 0 (MEMSS_PMA read failed) - reporting the raw +1
            // beats silently zeroing a valid signal.
            if (trainedMax > 0 && ratio > trainedMax)
                ratio = trainedMax;
            liveSource = ImcSource.MchbarLiveGvArl;
        }
        else
        {
            if (!ReadSaPerfStatusCoreUltra(out raw, out ratio))
                return false;
            liveSource = ImcSource.MchbarSaPerf;
        }

        ImcClockFlags flags = ImcClockFlags.LiveCurrent;
        if (!_validated)
            flags |= ImcClockFlags.Experimental;

        clock = new ImcClock(
            AbiVersion,
            liveSource,
            ratio,
            ImcReferenceClock.BclkDiv3,
            gear,
            raw,
            flags);
        return true;
    }

    /// <summary>
    /// Converts the wrapper's reference-clock enum into MHz using the BCLK
    /// the consumer has measured. Mirrors the IMC_REF_* table in the original
    /// IntelIMC.p driver.
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

    private bool ReadMchbarDword(int offset, out uint value)
    {
        value = 0;
        if (!_pawnIO.IsLoaded)
            return false;

        _readDwordIn[0] = offset;
        try
        {
            int hr = _pawnIO.ExecuteHr(IoctlReadDword, _readDwordIn, 1, _readDwordOut, 1, out uint returnSize);
            if (hr != 0 || returnSize < 1)
                return false;

            value = (uint)_readDwordOut[0];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool ReadMemssPma(out uint raw, out uint ratio, out ImcGear gear)
    {
        ratio = 0;
        gear = ImcGear.Unknown;

        if (!ReadMchbarDword(MEMSS_PMA_CR_BIOS_DATA, out raw))
            return false;

        if (_pmaReservedMask != 0 && (raw & _pmaReservedMask) != 0)
            return false;

        ratio = raw & 0xFF;
        gear = ((raw >> 8) & 0x1) != 0 ? ImcGear.Gear4 : ImcGear.Gear2;

        if (ratio < IMC_RATIO_MIN || ratio > IMC_RATIO_MAX)
            return false;

        return true;
    }

    private bool ReadSaPerfStatus(out uint raw, out uint ratio, out ImcReferenceClock refMode)
    {
        ratio = 0;
        refMode = ImcReferenceClock.Unknown;

        if (!ReadMchbarDword(SA_PERF_STATUS, out raw))
            return false;

        ratio = (raw >> 2) & 0xFF;
        refMode = ((raw >> 10) & 0x1) != 0 ? ImcReferenceClock.Bclk : ImcReferenceClock.BclkMul4Div3;

        if (ratio < IMC_RATIO_MIN || ratio > IMC_RATIO_MAX)
            return false;

        return true;
    }

    private bool ReadSaPerfStatusCoreUltra(out uint raw, out uint ratio)
    {
        ratio = 0;
        if (!ReadMchbarDword(SA_PERF_STATUS, out raw))
            return false;

        ratio = (raw >> 2) & 0xFF;
        if (ratio < IMC_RATIO_MIN || ratio > IMC_RATIO_MAX)
            return false;

        return true;
    }

    // ADL/RPL gear probe. MC_BIOS_DATA bits [11:8] encode the gear-down
    // stage (0=Gear1, 1=Gear2, 2=Gear4) - so the gear value is 2^stage.
    // Returns true only when a recognized stage is read; on any other case
    // (read failure, future/unknown stage) gear stays Unknown and the caller
    // proceeds without populating Memory Data Rate / DRAM Frequency.
    private bool TryReadAdlRplGear(out ImcGear gear)
    {
        gear = ImcGear.Unknown;
        if (!ReadMchbarDword(MC_BIOS_DATA_ADL_RPL, out uint raw))
            return false;

        uint stage = (raw >> 8) & 0xF;
        switch (stage)
        {
            case 0:
                gear = ImcGear.Gear1;
                return true;
            case 1:
                gear = ImcGear.Gear2;
                return true;
            case 2:
                gear = ImcGear.Gear4;
                return true;
            default:
                return false;
        }
    }

    private bool ReadArlLiveGvStatus(out uint raw, out uint ratio)
    {
        ratio = 0;
        if (!ReadMchbarDword(IMC_LIVE_GV_STATUS_ARL, out raw))
            return false;

        ratio = raw & 0xFF;
        if (ratio < IMC_RATIO_MIN || ratio > IMC_RATIO_MAX)
            return false;

        return true;
    }

    private static Platform ResolvePlatform()
    {
        var cpuId = OpCode.CpuId;
        if (cpuId == null)
            return Platform.None;

        if (!cpuId(1, 0, out uint eax, out _, out _, out _))
            return Platform.None;

        uint family = ((eax >> 8) & 0xF) + ((eax >> 20) & 0xFF);
        uint model = ((eax >> 4) & 0xF) | (((eax >> 16) & 0xF) << 4);
        return GetPlatform(family, model);
    }

    // CPUID family/model -> register-layout family. Mirrors get_platform()
    // in the original IntelIMC.p. NVL on Family 18 (Linux IFM(18, 0x01/03)).
    private static Platform GetPlatform(uint family, uint model)
    {
        if (family == 0x6)
        {
            switch (model)
            {
                case 0xAA:
                case 0xAC:
                case 0xB5:
                    return Platform.Mtl;
                case 0xC5:
                case 0xC6:
                    return Platform.Arl;
                case 0xBD:
                    return Platform.Lnl;
                case 0xCC:
                case 0xD5:
                    return Platform.Ptl;
                case 0x97:
                case 0x9A:
                case 0xBE:
                    return Platform.Adl;
                case 0xB7:
                case 0xBA:
                case 0xBF:
                    return Platform.Rpl;
            }
        }
        else if (family == 0x12)
        {
            // Nova Lake - INTEL_NOVALAKE = IFM(18, 0x01),
            //             INTEL_NOVALAKE_L = IFM(18, 0x03).
            switch (model)
            {
                case 0x01:
                case 0x03:
                    return Platform.Nvl;
            }
        }
        return Platform.None;
    }

    // Core Ultra (incl. NVL, experimental) -> MEMSS_PMA. ADL/RPL ->
    // SA_PERF_STATUS. No fall-through between sources.
    private static ImcSource GetPlatformSource(Platform platform)
    {
        return platform switch
        {
            Platform.Mtl or Platform.Arl or Platform.Lnl or Platform.Ptl or Platform.Nvl => ImcSource.MchbarMemssPma,
            Platform.Adl or Platform.Rpl => ImcSource.MchbarSaPerf,
            _ => ImcSource.None,
        };
    }

    // MTL/ARL match the 200H/200U layout (strict). PTL/LNL/NVL set bits in
    // the documented reserved range, so the gate is disabled and ratio-range
    // carries the consistency burden.
    private static uint GetPlatformPmaReservedMask(Platform platform)
    {
        return platform switch
        {
            Platform.Mtl or Platform.Arl => MEMSS_PMA_RESERVED_MASK_STRICT,
            Platform.Ptl or Platform.Lnl or Platform.Nvl => MEMSS_PMA_RESERVED_MASK_NONE,
            _ => MEMSS_PMA_RESERVED_MASK_STRICT,
        };
    }

    // ARL/PTL/LNL are cross-validated against HWiNFO/CPU-Z. MTL/ADL/RPL/NVL
    // are accepted but flagged EXPERIMENTAL until hardware validation lands.
    private static bool IsPlatformValidated(Platform platform)
    {
        return platform is Platform.Arl or Platform.Ptl or Platform.Lnl;
    }
}
