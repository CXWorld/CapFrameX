namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// Reads fabric-clock telemetry (D2D, NGU) on Arrow Lake-S and later
/// Intel CPUs via the OC Mailbox protocol on the new MSR pair
/// 0x607 (interface) + 0x608 (data).
/// </summary>
/// <remarks>
/// <para>
/// On Arrow Lake-S desktop the OC Mailbox moved off the legacy
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
/// <item><c>0x0022</c> — NGU ratio ((response &gt;&gt; 8) &amp; 0xFF), × 100 MHz.</item>
/// </list>
/// <para>
/// This is intentionally a thin protocol wrapper around
/// <see cref="IntelMsr"/>. All transport happens through that
/// class's <c>ReadMsr</c>/<c>WriteMsr</c> primitives, which gate
/// the two MSR addresses through the PawnIo IntelMSR module's
/// allow-list (extended for 0x607 / 0x608 in this CapFrameX
/// build, pending upstream PR to namazso/PawnIO-Modules).
/// </para>
/// </remarks>
public class IntelOcMailbox
{
    /// <summary>One sample of the SoC-fabric clocks read via OC Mailbox.</summary>
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

    private const uint MsrInterface = 0x607;
    private const uint MsrData = 0x608;
    private const ulong RunBit = 0x80000000UL;

    private const uint CmdD2d = 0x1237;
    private const uint CmdNgu = 0x0022;

    // Plausibility window applied to decoded ratios. Numbers outside
    // this almost certainly mean the mailbox returned junk (run-bit
    // didn't clear in time, or the CPU doesn't actually implement
    // this command).
    private const uint MinMhz = 100;
    private const uint MaxMhz = 10000;

    private const int PollMax = 1000;

    private readonly IntelMsr _msr;
    private readonly bool _isReady;

    /// <summary>
    /// Creates a new <see cref="IntelOcMailbox"/>. Probes MSR 0x607
    /// and MSR 0x608: if either reads back successfully the mailbox
    /// is considered present.
    /// </summary>
    public IntelOcMailbox(IntelMsr msr)
    {
        _msr = msr;
        if (msr == null)
            return;

        // The mailbox interface and data MSRs both exist on platforms
        // that implement this channel. Treat readability as the
        // presence test — wrong MSR returns #GP which surfaces as
        // ReadMsr=false.
        bool ifOk = msr.ReadMsr(MsrInterface, out _);
        bool dataOk = msr.ReadMsr(MsrData, out _);
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
            uint ratio = (uint)((rawNgu >> 8) & 0xFF);
            nguMhz = ratio * 100;
            if (nguMhz < MinMhz || nguMhz > MaxMhz)
                gotNgu = false;
        }

        if (!gotD2d && !gotNgu)
            return false;

        sample = new Sample(gotNgu, nguMhz, rawNgu, gotD2d, d2dMhz, rawD2d);
        return true;
    }

    /// <summary>
    /// Execute one OC Mailbox command. The protocol is non-atomic
    /// at the kernel boundary (three separate IOCTLs), so a
    /// concurrent writer can race us. In practice this manifests
    /// only as a one-cycle stale value before the next refresh
    /// corrects it.
    /// </summary>
    private bool TryExecute(uint command, out ulong data)
    {
        data = 0;
        if (!_msr.WriteMsr(MsrInterface, command | RunBit))
            return false;

        for (int i = 0; i < PollMax; i++)
        {
            if (!_msr.ReadMsr(MsrInterface, out ulong status))
                return false;
            if ((status & RunBit) == 0)
                return _msr.ReadMsr(MsrData, out data);
        }
        return false;
    }
}
