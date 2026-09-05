namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// Reads fabric-clock telemetry (D2D, NGU) on Arrow Lake-S and later
/// Intel CPUs via the VR Mailbox protocol on the new MSR pair
/// 0x607 (interface) + 0x608 (data).
/// </summary>
/// <remarks>
/// <para>
/// On Arrow Lake-S desktop the VR Mailbox moved off the legacy
/// MSR 0x150 to a new pair. This class implements the protocol
/// HWiNFO64 v8.32 also uses (RE-confirmed 2026-05-12):
/// </para>
/// <list type="number">
/// <item>WrMSR(0x607, command | 0x80000000)  — issue command + set run bit.</item>
/// <item>Poll RdMSR(0x607) until bit 31 (run bit) clears.</item>
/// <item>RdMSR(0x608) — read response data.</item>
/// </list>
/// <para>Known command codes:</para>
/// <list type="bullet">
/// <item><c>0x1237</c> — D2D ratio (response &amp; 0x7FFF), × 100 MHz.</item>
/// <item><c>0x0022</c> — NGU ratio candidate in bits 15:8, × 100 MHz.</item>
/// </list>
/// <para>
/// The NGU candidate is validated against OC Mailbox command <c>0x10</c>,
/// domain <c>7</c>, on legacy MSR <c>0x150</c>. Arrow Lake can return
/// <c>0x3F</c> from command <c>0x0022</c>; this is a sentinel. Callers
/// may supply a platform-specific fallback ratio. Without one, an
/// unresolved sentinel does not produce an NGU clock. This validation
/// and sentinel-fallback sequence mirrors HWiNFO64 v8.32.
/// </para>
/// <para>
/// This is intentionally a thin protocol wrapper around
/// <see cref="IntelMsr"/>. All transport happens through that
/// class's <c>ReadMsr</c>/<c>WriteMsr</c> primitives, which gate
/// the two MSR addresses through the PawnIo IntelMSR module's
/// allow-list (<c>MSR_VR_MAILBOX_INTERFACE</c> /
/// <c>MSR_VR_MAILBOX_DATA</c>).
/// </para>
/// </remarks>
public class IntelOcMailbox
{
    /// <summary>One sample of the SoC-fabric clocks read via VR Mailbox.</summary>
    public readonly struct Sample
    {
        /// <summary><c>true</c> when <see cref="NguMhz"/> is populated.</summary>
        public bool HasNgu { get; }
        /// <summary><c>true</c> when <see cref="D2dMhz"/> is populated.</summary>
        public bool HasD2d { get; }
        /// <summary>NGU clock in MHz (0 when not available).</summary>
        public uint NguMhz { get; }
        /// <summary>D2D clock in MHz (0 when not available).</summary>
        public uint D2dMhz { get; }
        /// <summary>Raw 64-bit response data for NGU (diagnostics).</summary>
        public ulong RawNgu { get; }
        /// <summary>Raw 64-bit response data for D2D (diagnostics).</summary>
        public ulong RawD2d { get; }

        internal Sample(bool hasNgu, uint nguMhz, ulong rawNgu,
                        bool hasD2d, uint d2dMhz, ulong rawD2d)
        {
            HasNgu = hasNgu;
            NguMhz = nguMhz;
            RawNgu = rawNgu;
            HasD2d = hasD2d;
            D2dMhz = d2dMhz;
            RawD2d = rawD2d;
        }
    }

    private const uint MSR_VR_MAILBOX_INTERFACE = 0x00000607;
    private const uint MSR_VR_MAILBOX_DATA = 0x00000608;
    private const uint MSR_OC_MAILBOX = 0x00000150;
    private const ulong RunBit = 0x80000000UL;
    private const ulong LegacyRunBit = 0x8000000000000000UL;

    private const uint CmdD2d = 0x1237;
    private const uint CmdNgu = 0x0022;
    private const ulong CmdLegacyNguRatio = LegacyRunBit | (0x10UL << 32) | (0x07UL << 40);
    private const uint NguRatioSentinel = 0x3F;

    // Plausibility window applied to decoded ratios. Numbers outside
    // this almost certainly mean the mailbox returned junk (run-bit
    // didn't clear in time, or the CPU doesn't actually implement
    // this command).
    private const uint MinMhz = 100;
    private const uint MaxMhz = 10000;

    private const int PollMax = 1000;

    private readonly IntelMsr _msr;
    private readonly bool _isReady;
    private readonly uint? _nguSentinelFallbackRatio;

    /// <summary>
    /// Creates a new <see cref="IntelOcMailbox"/>. Probes MSR 0x607
    /// and MSR 0x608: if both read back successfully the mailbox
    /// is considered present.
    /// </summary>
    /// <param name="msr">MSR transport.</param>
    /// <param name="nguSentinelFallbackRatio">
    /// Platform-specific NGU ratio used when command 0x0022 returns
    /// the unresolved 0x3F sentinel; <c>null</c> suppresses the clock.
    /// </param>
    public IntelOcMailbox(IntelMsr msr, uint? nguSentinelFallbackRatio)
    {
        _msr = msr;
        _nguSentinelFallbackRatio = nguSentinelFallbackRatio;
        if (msr == null)
            return;

        // The mailbox interface and data MSRs both exist on platforms
        // that implement this channel. Treat readability as the
        // presence test — wrong MSR returns #GP which surfaces as
        // ReadMsr=false.
        bool ifOk = msr.ReadMsr(MSR_VR_MAILBOX_INTERFACE, out _);
        bool dataOk = msr.ReadMsr(MSR_VR_MAILBOX_DATA, out _);
        _isReady = ifOk && dataOk;
    }

    /// <summary><c>true</c> when MSR 0x607/0x608 are both readable on this CPU.</summary>
    public bool IsReady => _isReady;

    /// <summary>
    /// Reads D2D and NGU via two mailbox commands. Returns
    /// <c>false</c> if neither succeeded or both produced values
    /// outside the plausibility window.
    /// </summary>
    public bool TryRead(out Sample sample)
    {
        sample = default;
        if (!_isReady)
            return false;

        bool gotD2d = TryExecute(CmdD2d, out ulong rawD2d);
        bool gotNgu = TryExecute(CmdNgu, out ulong rawNgu);

        uint d2dMhz = 0;
        if (gotD2d)
        {
            uint ratio = (uint)(rawD2d & 0x7FFF);
            d2dMhz = ratio * 100;
            if (d2dMhz < MinMhz || d2dMhz > MaxMhz)
                gotD2d = false;
        }

        uint nguMhz = 0;
        if (gotNgu)
        {
            uint ratioLimit = 0;
            TryReadLegacyNguRatio(out ratioLimit);
            gotNgu = TryDecodeNguMhz(rawNgu, ratioLimit, _nguSentinelFallbackRatio, out nguMhz);
        }

        if (!gotD2d && !gotNgu)
            return false;

        sample = new Sample(gotNgu, nguMhz, rawNgu, gotD2d, d2dMhz, rawD2d);
        return true;
    }

    internal static bool TryDecodeNguMhz(
        ulong raw,
        uint ratioLimit,
        uint? sentinelFallbackRatio,
        out uint mhz)
    {
        uint ratio = (uint)((raw >> 8) & 0xFF);

        if (ratioLimit > 0 && ratioLimit < ratio)
            ratio = ratioLimit;

        // HWiNFO applies its platform fallback after the legacy mailbox
        // validation. Without equivalent platform knowledge, do not
        // report the unresolved 0x3F sentinel as a 6300 MHz clock.
        if (ratio == NguRatioSentinel)
        {
            if (!sentinelFallbackRatio.HasValue)
            {
                mhz = 0;
                return false;
            }

            ratio = sentinelFallbackRatio.Value;
        }

        mhz = ratio * 100;
        return mhz >= MinMhz && mhz <= MaxMhz;
    }

    /// <summary>
    /// Reads the NGU ratio through the legacy OC Mailbox. The read-only
    /// request uses command 0x10, domain 7 and parameter 0 on MSR 0x150.
    /// </summary>
    private bool TryReadLegacyNguRatio(out uint ratio)
    {
        ratio = 0;

        if (!WaitForLegacyMailbox(out _))
            return false;

        if (!_msr.WriteMsr(MSR_OC_MAILBOX, CmdLegacyNguRatio))
            return false;

        if (!WaitForLegacyMailbox(out ulong response))
            return false;

        uint completionCode = (uint)((response >> 32) & 0xFF);
        if (completionCode != 0)
            return false;

        ratio = (uint)(response & 0xFF);
        return ratio != 0;
    }

    private bool WaitForLegacyMailbox(out ulong response)
    {
        response = 0;
        for (int i = 0; i < PollMax; i++)
        {
            if (!_msr.ReadMsr(MSR_OC_MAILBOX, out response))
                return false;
            if ((response & LegacyRunBit) == 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Execute one VR Mailbox command. The protocol is non-atomic
    /// at the kernel boundary (three separate IOCTLs), so a
    /// concurrent writer can race us. In practice this manifests
    /// only as a one-cycle stale value before the next refresh
    /// corrects it.
    /// </summary>
    private bool TryExecute(uint command, out ulong data)
    {
        data = 0;
        if (!_msr.WriteMsr(MSR_VR_MAILBOX_INTERFACE, command | RunBit))
            return false;

        for (int i = 0; i < PollMax; i++)
        {
            if (!_msr.ReadMsr(MSR_VR_MAILBOX_INTERFACE, out ulong status))
                return false;
            if ((status & RunBit) == 0)
                return _msr.ReadMsr(MSR_VR_MAILBOX_DATA, out data);
        }
        return false;
    }
}
