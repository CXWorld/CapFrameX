using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// Provides typed access to the Intel Out-Of-Band Management Services
/// Module (OOBMSM) on Core Ultra (Meteor Lake and later) tile-based
/// CPUs. OOBMSM is the SoC-tile telemetry/management aggregator that
/// hosts Intel Platform Monitoring Technology (PMT) sub-functions
/// (Telemetry, Watcher, Crashlog, SDSi, TPMI) as PCIe extended
/// capabilities.
/// </summary>
/// <remarks>
/// <para>
/// The kernel-side <c>IntelOOBMSM.bin</c> is intentionally minimal —
/// it only validates that we are on a supported Intel CPU and exposes
/// two primitives: a PCI-config DWORD read against the canonical
/// <c>00:0A.0</c> slot, and a physical-memory DWORD read. All
/// capability discovery, BAR-base resolution and per-platform
/// telemetry-layout decoding happens here in C#.
/// </para>
/// <para>
/// This mirrors the split established by <see cref="IntelImc"/>
/// against <c>IntelMCHBAR.bin</c>: kernel = primitives, C# = decode.
/// </para>
/// </remarks>
public class IntelOobmsm
{
    /// <summary>Common Intel ext-cap IDs used by OOBMSM.</summary>
    public enum ExtCapKind : ushort
    {
        /// <summary>Unspecified (cap_id is in <see cref="ExtCapEntry.CapId"/>).</summary>
        Unknown = 0,
        /// <summary>PCIe Vendor-Specific Extended Capability (PMT VSEC family).</summary>
        Vsec = 0x000B,
        /// <summary>Intel Topology-Aware Power Management Interface.</summary>
        Tpmi = 0x0023,
    }

    /// <summary>
    /// Register-layout family the running CPU belongs to. Mirrors the
    /// kernel-side <c>get_code_name</c> allowlist; CPUs outside the
    /// allowlist resolve to <see cref="None"/>.
    /// </summary>
    public enum Platform
    {
        /// <summary>CPU is not a tile-based Core Ultra part.</summary>
        None = 0,
        /// <summary>Meteor Lake (Family 6, models 0xAC/0xAA).</summary>
        Mtl,
        /// <summary>Arrow Lake (Family 6, models 0xC5/0xC6/0xB5).</summary>
        Arl,
        /// <summary>Lunar Lake (Family 6, model 0xBD).</summary>
        Lnl,
        /// <summary>Panther Lake (Family 6, model 0xCC).</summary>
        Ptl,
        /// <summary>Wildcat Lake (Family 6, model 0xD5).</summary>
        Wcl,
        /// <summary>Nova Lake (Family 18, models 0x01/0x03).</summary>
        Nvl,
    }

    /// <summary>One entry in OOBMSM's PCIe ext-cap chain (decoded in C#).</summary>
    public readonly struct ExtCapEntry
    {
        /// <summary>Byte offset of the ext-cap header in PCI config space (≥ 0x100).</summary>
        public int ConfigOffset { get; }
        /// <summary>PCIe extended-capability ID (e.g. <c>0x000B</c> = VSEC, <c>0x0023</c> = TPMI).</summary>
        public ushort CapId { get; }
        /// <summary>Capability version (0..15, from bits [19:16] of the header).</summary>
        public byte CapVersion { get; }
        /// <summary>VSEC ID (low 16 bits of the dword at <c>ConfigOffset + 4</c>).</summary>
        public ushort VsecId { get; }
        /// <summary>VSEC revision (4 bits at <c>ConfigOffset + 4</c>, [19:16]).</summary>
        public byte VsecRevision { get; }
        /// <summary>VSEC length in bytes (12 bits at <c>ConfigOffset + 4</c>, [31:20]).</summary>
        public ushort VsecLength { get; }
        /// <summary>BAR Indicator (low 3 bits of the dword at <c>ConfigOffset + 8</c>).</summary>
        public int Tbir { get; }
        /// <summary>Discovery byte offset within the TBIR-selected BAR (bits [31:3] of the dword at <c>ConfigOffset + 8</c>).</summary>
        public uint DiscoveryOffset { get; }

        internal ExtCapEntry(int configOffset, ushort capId, byte capVersion,
                             ushort vsecId, byte vsecRev, ushort vsecLength,
                             int tbir, uint discoveryOffset)
        {
            ConfigOffset = configOffset;
            CapId = capId;
            CapVersion = capVersion;
            VsecId = vsecId;
            VsecRevision = vsecRev;
            VsecLength = vsecLength;
            Tbir = tbir;
            DiscoveryOffset = discoveryOffset;
        }
    }

    private const string IoctlPciConfigReadDword = "ioctl_pci_config_read_dword";
    private const string IoctlReadDword = "ioctl_read_dword";
    private const string IoctlIdentity = "ioctl_identity";

    private const int PciCfgVendorId = 0x00;
    private const int PciCfgBar0Lo = 0x10;
    private const int PciCfgSpaceExpSize = 0x1000;
    private const int PciExtCapListBase = 0x100;
    private const int MaxExtCapWalk = 64;

    // The kernel module's IOCTLs always use a fixed 1-cell input/
    // output for our cases; pre-allocate buffers to avoid garbage on
    // every call. Empty input is `_in0`, single is `_in1`.
    private static readonly long[] _in0 = Array.Empty<long>();
    private readonly long[] _in1 = new long[1];
    private readonly long[] _out1 = new long[1];
    private readonly long[] _out2 = new long[2];
    private readonly long[] _out4 = new long[4];

    private readonly PawnIo _pawnIO = PawnIo.LoadModuleFromResource(typeof(IntelOobmsm).Assembly, $"{nameof(LibreHardwareMonitor)}.Resources.PawnIO.IntelOOBMSM.bin");

    private readonly Platform _platform;
    private readonly bool _validated;
    private ExtCapEntry[] _extCapsCache;

    /// <summary>
    /// Loads the OOBMSM PawnIO module and resolves the running CPU
    /// to a register-layout family. On unsupported CPUs the instance
    /// still constructs but every <c>Try*</c> method returns false.
    /// </summary>
    public IntelOobmsm()
    {
        _platform = ResolvePlatform();
        _validated = IsPlatformValidated(_platform);
    }

    /// <summary><c>true</c> when the kernel module loaded and the running CPU is in the allowlist.</summary>
    public bool IsLoaded => _pawnIO.IsLoaded && _platform != Platform.None;

    /// <summary>Detected register-layout family.</summary>
    public Platform DetectedPlatform => _platform;

    /// <summary><c>true</c> when this platform has been cross-validated against reference telemetry.</summary>
    public bool IsValidated => _validated;

    /// <summary>Closes the underlying PawnIO module handle.</summary>
    public void Close() => _pawnIO.Close();

    /// <summary>
    /// Reads OOBMSM identity (PCI VID/DID + bus/dev/fn) and the
    /// physical address / size of the kernel-mapped BAR0 window.
    /// </summary>
    public bool TryGetIdentity(out ushort vendorId, out ushort deviceId, out byte bus, out byte device, out byte function, out ulong barPhysical, out int barSize)
    {
        vendorId = 0;
        deviceId = 0;
        bus = 0;
        device = 0;
        function = 0;
        barPhysical = 0;
        barSize = 0;
        if (!IsLoaded)
            return false;
        if (!Execute(IoctlIdentity, _in0, 0, _out4, 4))
            return false;
        uint vidDid = (uint)_out4[0];
        vendorId = (ushort)(vidDid & 0xFFFF);
        deviceId = (ushort)((vidDid >> 16) & 0xFFFF);
        long bdf = _out4[1];
        function = (byte)(bdf & 0xFF);
        device = (byte)((bdf >> 8) & 0xFF);
        bus = (byte)((bdf >> 16) & 0xFF);
        barPhysical = (ulong)_out4[2];
        barSize = (int)_out4[3];
        return vendorId == 0x8086 && vendorId != 0xFFFF;
    }

    /// <summary>
    /// Reads a 32-bit DWORD from OOBMSM's PCI configuration space.
    /// </summary>
    /// <param name="offset">Byte offset, DW-aligned, in <c>[0, 0xFFC]</c>.</param>
    /// <param name="value">Value read.</param>
    public bool TryReadConfigDword(int offset, out uint value)
    {
        value = 0;
        if (!IsLoaded)
            return false;
        _in1[0] = offset;
        if (!Execute(IoctlPciConfigReadDword, _in1, 1, _out1, 1))
            return false;
        value = (uint)_out1[0];
        return true;
    }

    /// <summary>
    /// Reads a 32-bit DWORD from the kernel-mapped BAR0 window of
    /// OOBMSM at the given byte offset within that window.
    /// </summary>
    /// <remarks>
    /// The kernel module maps OOBMSM's BAR0 (typically 64 KiB on
    /// shipping Core Ultra parts) at init via <c>io_space_map</c>;
    /// this primitive reads a DWORD inside that mapping. All TPMI
    /// telemetry apertures we have observed live within the upper
    /// part of BAR0 (0xF800+), so the full 64 KiB window covers any
    /// per-platform discovery offset.
    /// </remarks>
    public bool TryReadBarDword(int offset, out uint value)
    {
        value = 0;
        if (!IsLoaded || offset < 0)
            return false;
        _in1[0] = offset;
        if (!Execute(IoctlReadDword, _in1, 1, _out1, 1))
            return false;
        value = (uint)_out1[0];
        return true;
    }

    /// <summary>
    /// Walks the PCIe extended-capability chain starting at offset
    /// 0x100 of OOBMSM's config space and returns every entry found.
    /// Result is cached for the lifetime of this instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TBIR + discovery-offset position varies by capability flavour:
    /// </para>
    /// <list type="bullet">
    /// <item><b>VSEC (cap_id 0x000B)</b>: BIR + offset packed in the
    /// dword at <c>ConfigOffset + 0x08</c> — bits[2:0] = TBIR,
    /// bits[31:3] = byte offset within the BAR.</item>
    /// <item><b>TPMI (cap_id 0x0023)</b>: same layout but at
    /// <c>ConfigOffset + 0x0C</c> (after a 4-byte DVSEC ID slot at
    /// +0x08).</item>
    /// </list>
    /// <para>
    /// Caps that don't expose a parsable entry surface with
    /// <c>Tbir = -1</c>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<ExtCapEntry> EnumerateExtCaps()
    {
        if (_extCapsCache != null)
            return _extCapsCache;
        if (!IsLoaded)
        {
            _extCapsCache = Array.Empty<ExtCapEntry>();
            return _extCapsCache;
        }

        var list = new List<ExtCapEntry>(8);
        int offset = PciExtCapListBase;
        for (int i = 0; i < MaxExtCapWalk && offset != 0; i++)
        {
            if (offset < PciExtCapListBase || offset >= PciCfgSpaceExpSize)
                break;
            if (!TryReadConfigDword(offset, out uint hdr) || hdr == 0 || hdr == 0xFFFFFFFF)
                break;
            ushort capId = (ushort)(hdr & 0xFFFF);
            byte capVer = (byte)((hdr >> 16) & 0xF);
            int next = (int)((hdr >> 20) & 0xFFC);

            ushort vsecId = 0;
            byte vsecRev = 0;
            ushort vsecLen = 0;
            int tbir = -1;
            uint disc = 0;
            if (TryReadConfigDword(offset + 4, out uint vsecHdr))
            {
                vsecId = (ushort)(vsecHdr & 0xFFFF);
                vsecRev = (byte)((vsecHdr >> 16) & 0xF);
                vsecLen = (ushort)((vsecHdr >> 20) & 0xFFF);
            }
            // Entry header is at +0x08 for legacy VSEC, +0x0C for TPMI.
            int entryOffset = capId == (ushort)ExtCapKind.Tpmi ? 0x0C : 0x08;
            if (TryReadConfigDword(offset + entryOffset, out uint entryHdr))
            {
                tbir = (int)(entryHdr & 0x7);
                disc = entryHdr & 0xFFFFFFF8u;
            }

            list.Add(new ExtCapEntry(offset, capId, capVer, vsecId, vsecRev, vsecLen, tbir, disc));

            if (next == offset)
                break;
            offset = next;
        }

        _extCapsCache = list.ToArray();
        return _extCapsCache;
    }

    /// <summary>
    /// Looks up the first ext-cap entry matching the requested cap-id
    /// (and optionally a vsec-id within VSEC/TPMI caps). Returns
    /// <c>false</c> if nothing matches.
    /// </summary>
    public bool TryFindExtCap(ushort capId, out ExtCapEntry entry)
        => TryFindExtCap(capId, vsecId: null, out entry);

    /// <inheritdoc cref="TryFindExtCap(ushort, out ExtCapEntry)"/>
    /// <param name="vsecId">Optional VSEC ID filter (low 16 bits of the dword at <c>ConfigOffset + 4</c>).</param>
    public bool TryFindExtCap(ushort capId, ushort? vsecId, out ExtCapEntry entry)
    {
        entry = default;
        foreach (var c in EnumerateExtCaps())
        {
            if (c.CapId != capId)
                continue;
            if (vsecId.HasValue && c.VsecId != vsecId.Value)
                continue;
            entry = c;
            return true;
        }
        return false;
    }

    private bool Execute(string name, long[] inBuf, int inSize, long[] outBuf, int outSize)
    {
        if (!_pawnIO.IsLoaded)
            return false;
        try
        {
            int hr = _pawnIO.ExecuteHr(name, inBuf, (uint)inSize, outBuf, (uint)outSize, out uint returnSize);
            return hr == 0 && returnSize >= outSize;
        }
        catch
        {
            return false;
        }
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

    private static Platform GetPlatform(uint family, uint model)
    {
        if (family == 0x6)
        {
            switch (model)
            {
                case 0xAC:
                case 0xAA:
                    return Platform.Mtl;
                case 0xC5:
                case 0xC6:
                case 0xB5:
                    return Platform.Arl;
                case 0xBD:
                    return Platform.Lnl;
                case 0xCC:
                    return Platform.Ptl;
                case 0xD5:
                    return Platform.Wcl;
            }
        }
        else if (family == 0x12)
        {
            switch (model)
            {
                case 0x01:
                case 0x03:
                    return Platform.Nvl;
            }
        }
        return Platform.None;
    }

    private static bool IsPlatformValidated(Platform platform)
    {
        return platform is Platform.Arl or Platform.Ptl or Platform.Lnl;
    }
}
