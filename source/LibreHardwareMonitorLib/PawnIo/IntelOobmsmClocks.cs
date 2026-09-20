using System;

namespace LibreHardwareMonitor.PawnIo;

/// <summary>
/// Decodes the SoC-fabric clock fields (NGU and D2D) exposed by the
/// Intel OOBMSM PMT Telemetry block on Core Ultra (Meteor Lake and
/// later) tile-based CPUs.
/// </summary>
/// <remarks>
/// <para>
/// All platform dispatch (telemetry-container offset, bit position,
/// frequency multiplier) lives here. The <see cref="IntelOobmsm"/>
/// primitive layer below this exposes only PCI-config + physical-
/// memory DWORD reads; this class does the ext-cap walk, BAR-base
/// resolution, and bit-field decoding.
/// </para>
/// <para>
/// <b>Offset source: <c>github.com/intel/Intel-PMT</c></b>. Each
/// platform's <c>*_aggregator.xml</c> defines bit-packed sample
/// containers identified by their byte offset within the discovery
/// aperture (the "Groupname 0xXXXX" descriptions). NGU and D2D are
/// sub-byte slices inside an 8-byte container with a per-field MHz
/// multiplier (<c>tratio_50</c> = ratio × 50 MHz, <c>tratio_100</c>
/// = ratio × 100 MHz, etc.).
/// </para>
/// </remarks>
public class IntelOobmsmClocks
{
    /// <summary>One field inside a PMT telemetry container.</summary>
    public readonly struct ClockField
    {
        /// <summary>Byte offset of the 8-byte container within the discovery aperture (e.g. 0x82F8 for LNL Container_2).</summary>
        public uint ContainerOffset { get; }
        /// <summary>Bit lsb within the 64-bit container (0..63).</summary>
        public byte BitLsb { get; }
        /// <summary>Bit msb (inclusive) within the 64-bit container (0..63).</summary>
        public byte BitMsb { get; }
        /// <summary>Frequency unit per ratio step, in MHz (50, 100, 33, …).</summary>
        public ushort MultiplierMhz { get; }

        /// <summary><c>true</c> when this field is configured (non-zero).</summary>
        public bool IsConfigured => ContainerOffset != 0 && MultiplierMhz != 0 && BitMsb >= BitLsb;

        /// <summary>Initializes a new instance of the <see cref="ClockField"/> struct.</summary>
        /// <param name="containerOffset">Byte offset of the 8-byte container within the discovery aperture.</param>
        /// <param name="bitLsb">Bit lsb within the 64-bit container (0..63).</param>
        /// <param name="bitMsb">Bit msb (inclusive) within the 64-bit container (0..63).</param>
        /// <param name="multiplierMhz">Frequency unit per ratio step, in MHz.</param>
        public ClockField(uint containerOffset, byte bitLsb, byte bitMsb, ushort multiplierMhz)
        {
            ContainerOffset = containerOffset;
            BitLsb = bitLsb;
            BitMsb = bitMsb;
            MultiplierMhz = multiplierMhz;
        }
    }

    /// <summary>One sample of the SoC-fabric clocks for the running platform.</summary>
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
        /// <summary>Raw 64-bit container reading at the NGU field (diagnostics).</summary>
        public ulong RawNgu { get; }
        /// <summary>Raw 64-bit container reading at the D2D field (diagnostics).</summary>
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

    private readonly struct PlatformOffsets
    {
        public ClockField Ngu { get; }
        public ClockField D2d { get; }
        public bool Validated { get; }

        public PlatformOffsets(ClockField ngu, ClockField d2d, bool validated)
        {
            Ngu = ngu;
            D2d = d2d;
            Validated = validated;
        }
    }

    // Plausibility window in MHz applied to decoded samples. Outside
    // values almost certainly mean the field decode is wrong (bit
    // positions stale across a stepping, or the wrong container).
    // Range comfortably covers idle (~200 MHz) up to ~10 GHz fabric
    // ceilings.
    private const uint MinMhz = 100;
    private const uint MaxMhz = 10000;

    // PMT GUIDs published in github.com/intel/Intel-PMT/xml/pmt.xml.
    // We pin the wrapper to the "normal Punit telemetry" sub-feature
    // (GUID 0x3086000 on PTL); its layout matches LNL's Container_2
    // at BAR-byte-offset 0x82F8 and exposes NCLK/NGU + D2D + MEMSS
    // QCLK + Display CDCLK in a single 8-byte container.
    private const uint GuidPtlNormalPunit = 0x03086000;

    // Inside the TPMI sub-feature discovery aperture, the first DWORD
    // is the GUID itself (Intel-PMT discovery-aperture convention).
    // We read it at the cap_offset reported by the TPMI PFS table to
    // confirm we actually have the expected sub-feature before we
    // start decoding container fields.
    private const int ApertureGuidOffset = 0;

    // The TPMI PFS table at TPMI[0].discovery_offset contains 16-byte
    // entries: dword[0] = packed metadata (TPMI ID, attribute, …),
    // dword[1] = GUID, dword[2] = byte offset within the BAR pointing
    // to the sub-feature's discovery aperture, dword[3] = reserved/CRC.
    // This wrapper iterates up to PfsMaxEntries entries to find the
    // one matching GuidPtlNormalPunit.
    private const int PfsEntrySize = 16;
    private const int PfsMaxEntries = 16;

    private readonly IntelOobmsm _oobmsm;
    private readonly IntelOobmsm.Platform _platform;
    private readonly bool _validated;
    private readonly bool _haveAperture;
    private readonly uint _matchedGuid;       // GUID confirmed at the sub-aperture base
    private readonly uint _subApertureOffset; // byte offset within BAR0 of the sub-aperture header
    private readonly PlatformOffsets _offsets;

    /// <summary>
    /// Creates a new <see cref="IntelOobmsmClocks"/>. Resolves the
    /// running CPU to a platform and locates the OOBMSM Punit
    /// telemetry aperture's absolute physical address via the C#-side
    /// ext-cap walker. Per-platform bit-field offsets come from
    /// <c>github.com/intel/Intel-PMT</c> XML metadata.
    /// </summary>
    public IntelOobmsmClocks(IntelOobmsm oobmsm)
    {
        _oobmsm = oobmsm;
        _platform = oobmsm?.DetectedPlatform ?? IntelOobmsm.Platform.None;
        _offsets = GetPlatformOffsets(_platform);
        _validated = _offsets.Validated;

        if (oobmsm == null || !oobmsm.IsLoaded || _platform == IntelOobmsm.Platform.None)
            return;

        // Find a TPMI cap that points at a non-zero PFS table base
        // within BAR0. (Multiple TPMI caps exist on PTL but only the
        // first two carry usable apertures; the rest are placeholders
        // with cap-pointer 0.)
        uint pfsBase = 0;
        foreach (var c in oobmsm.EnumerateExtCaps())
        {
            if (c.CapId != (ushort)IntelOobmsm.ExtCapKind.Tpmi)
                continue;
            if (c.Tbir != 0 || c.DiscoveryOffset == 0)
                continue;
            pfsBase = c.DiscoveryOffset;
            break;
        }
        if (pfsBase == 0)
            return;

        // Walk the PFS table looking for our sub-feature GUID. Each
        // entry is 16 bytes (4 dwords): [meta, GUID, cap_offset, crc].
        for (int i = 0; i < PfsMaxEntries; i++)
        {
            int entryBase = (int)pfsBase + i * PfsEntrySize;
            if (!oobmsm.TryReadBarDword(entryBase + 4, out uint guid))
                break;
            if (!oobmsm.TryReadBarDword(entryBase + 8, out uint capOffset))
                break;
            if (guid == 0 && capOffset == 0)
                continue; // empty/placeholder entry
            if (guid != GuidPtlNormalPunit)
                continue;
            if (capOffset == 0)
                continue;

            // Verify the sub-aperture header carries the same GUID
            // (PMT discovery-aperture convention: first DWORD = GUID).
            if (!oobmsm.TryReadBarDword((int)capOffset + ApertureGuidOffset, out uint headerGuid))
                continue;
            if (headerGuid != GuidPtlNormalPunit)
                continue;

            _matchedGuid = guid;
            _subApertureOffset = capOffset;
            _haveAperture = true;
            break;
        }
    }

    /// <summary><c>true</c> when at least one of NGU / D2D is configured for the running platform AND the aperture base resolved.</summary>
    public bool IsReady => _haveAperture && (_offsets.Ngu.IsConfigured || _offsets.D2d.IsConfigured);

    /// <summary><c>true</c> once the platform's offsets have been cross-validated against reference telemetry (HWiNFO).</summary>
    public bool IsValidated => _validated;

    /// <summary><c>true</c> when an NGU field is configured for this platform.</summary>
    public bool HasNgu => _offsets.Ngu.IsConfigured;

    /// <summary><c>true</c> when a D2D field is configured for this platform.</summary>
    public bool HasD2d => _offsets.D2d.IsConfigured;

    /// <summary>
    /// Byte offset of the matched Punit-telemetry sub-aperture
    /// within OOBMSM's BAR0 mapping. The first DWORD at this offset
    /// is the matched GUID. Containers in the platform table use
    /// absolute BAR-byte-offsets, not offsets relative to this base
    /// (see <see cref="ClockField.ContainerOffset"/>).
    /// </summary>
    public uint SubApertureBarOffset => _subApertureOffset;

    /// <summary>GUID confirmed at the sub-aperture header (e.g. <c>0x03086000</c> for PTL normal Punit telemetry).</summary>
    public uint MatchedGuid => _matchedGuid;

    /// <summary>
    /// Reads NGU and D2D in one pair of qword fetches. Returns
    /// <c>false</c> if neither field is configured or both reads
    /// failed. Values outside the plausibility window are dropped
    /// (treated as a decode mismatch rather than reported as live
    /// data).
    /// </summary>
    public bool TryRead(out Sample sample)
    {
        sample = default;
        if (!IsReady)
            return false;

        bool gotNgu = TryDecodeField(_offsets.Ngu, out uint nguMhz, out ulong rawNgu);
        bool gotD2d = TryDecodeField(_offsets.D2d, out uint d2dMhz, out ulong rawD2d);

        if (!gotNgu && !gotD2d)
            return false;
        sample = new Sample(gotNgu, nguMhz, rawNgu, gotD2d, d2dMhz, rawD2d);
        return true;
    }

    private bool TryDecodeField(ClockField field, out uint mhz, out ulong raw)
    {
        mhz = 0;
        raw = 0;
        if (!field.IsConfigured)
            return false;

        // Containers are 8 bytes (64 bits). The kernel only exposes a
        // DWORD-read primitive, so we fetch low and high halves
        // separately and combine. Container offsets in the platform
        // table are absolute byte offsets within BAR0 (not relative
        // to the sub-aperture base).
        int barOffsetLo = (int)field.ContainerOffset;
        int barOffsetHi = barOffsetLo + 4;
        if (!_oobmsm.TryReadBarDword(barOffsetLo, out uint lo))
            return false;
        if (!_oobmsm.TryReadBarDword(barOffsetHi, out uint hi))
            return false;
        raw = ((ulong)hi << 32) | lo;

        int width = field.BitMsb - field.BitLsb + 1;
        if (width <= 0 || width > 32)
            return false;
        ulong mask = width == 64 ? ulong.MaxValue : ((1UL << width) - 1UL);
        uint ratio = (uint)((raw >> field.BitLsb) & mask);
        uint val = ratio * field.MultiplierMhz;
        if (val < MinMhz || val > MaxMhz)
            return false;
        mhz = val;
        return true;
    }

    private static PlatformOffsets GetPlatformOffsets(IntelOobmsm.Platform platform)
    {
        // ============================================================
        // PER-PLATFORM NCLK / D2D FIELD TABLE
        // ============================================================
        // Source: github.com/intel/Intel-PMT/xml/<PLAT>/0/*_aggregator.xml
        //   - Container is identified by its "Groupname 0xXXXX" byte
        //     offset within the PMT-Telemetry discovery aperture.
        //   - Each container is 64 bits, packed with multiple fields
        //     (frequency, voltage, etc.) by lsb/msb bit slice.
        //   - Frequency datatype suffix gives the multiplier:
        //         tratio_50  -> ratio × 50  MHz
        //         tratio_100 -> ratio × 100 MHz
        //         tratio_33  -> ratio × 33  MHz
        //
        // What we actually read (despite the legacy "Ngu" field name):
        //   - The "Ngu" sensor exposes MAIN_NOC_FREQ ("NCLK frequency"
        //     in the LNL aggregator XML, sample id 18 in Container_2
        //     at byte offset 0x82F8). NCLK is the SoC-tile main NoC
        //     clock — what HWiNFO labels "NGU/NCLK". On PTL/LNL this
        //     domain is × 50 MHz, NOT × 100 MHz like Arrow Lake's
        //     compute-tile NGU. SkatterBencher's ARL "× 100 MHz, def
        //     26X = 2.6 GHz" applies to ARL Desktop only; LNL/PTL
        //     mobile silicon uses a separate × 50 MHz NCLK domain.
        //   - "D2D" exposes D2D_FREQ (sample id 20, bits 50..57,
        //     × 50 MHz). Per Intel's published XML "D2D frequency in
        //     units of 50MHz".
        //
        // Cross-platform naming differs:
        //   - MTL labels its main fabric clock "NGU FREQ" (Container
        //     0x6348 bits [48..55], × 100 MHz). MTL does not expose
        //     a distinct D2D field in the public Punit XML.
        //   - LNL labels the SoC main NoC clock "MAIN_NOC_FREQ" and
        //     exposes a separate "D2D_FREQ"; both live in Container_2
        //     at 0x82F8. lsb/msb come straight from the LNL
        //     aggregator XML — see the table below.
        //   - PTL keeps the LNL Container_2 layout binary-identically
        //     at 0x82F8 but every sub-field is publicly named
        //     "reserved" in the PTL XML. Decoded values match HWiNFO
        //     stable (NCLK 550 MHz, D2D 300 MHz on PTL idle), so we
        //     mark the platform validated.
        //   - ARL has no XML in the public Intel-PMT registry as of
        //     this writing; left empty.
        //   - WCL / NVL similarly absent from the registry.
        //
        // LNL Container_2 (Groupname 0x82F8) bit layout (verbatim XML):
        //   [ 0.. 7] MEMSS_FREQ     tratio_33     QCLK × 33 MHz
        //   [ 8..15] MEMSS_VOLTAGE  tshort_volts
        //   [16..25] DISP_FREQ      tclk_freq     CDCLK direct
        //   [26..33] DISP_VOLTAGE   tshort_volts
        //   [34..41] MAIN_NOC_FREQ  tratio_50     NCLK × 50 MHz  <-- our "Ngu"
        //   [42..49] MAIN_NOC_VOLT  tshort_volts
        //   [50..57] D2D_FREQ       tratio_50     × 50 MHz       <-- our "D2d"
        //   [58..63] reserved
        //
        // When a value is filled in but Validated stays false, the
        // wrapper still surfaces samples (subject to the plausibility
        // window), but consumers should hide the sensors from the
        // default presentation.
        // ============================================================
        switch (platform)
        {
            case IntelOobmsm.Platform.Mtl:
                return new PlatformOffsets(
                    ngu: new ClockField(0x6348, 48, 55, 100),
                    d2d: default,
                    validated: false);

            case IntelOobmsm.Platform.Lnl:
                return new PlatformOffsets(
                    ngu: new ClockField(0x82F8, 34, 41, 50),
                    d2d: new ClockField(0x82F8, 50, 57, 50),
                    validated: false);

            case IntelOobmsm.Platform.Ptl:
                // Cross-validated against HWiNFO64 stable v8.46 (build
                // 5960, 2026-04-15) which contains the documented PTL
                // NGU/D2D fix. PTL re-uses LNL's Container_2 layout
                // at 0x82F8 binary-identically even though the PTL
                // aggregator XML labels every sub-field "reserved".
                //
                // Verified dynamic range on PTL-L (Core Ultra Series 3
                // mobile):
                //   - NCLK is runtime-variable: ratio 11 × 50 = 550 MHz
                //     idle, ratio 24 × 50 = 1200 MHz under UI load
                //     (CapFrameX polling).
                //   - D2D is boot-fixed at ratio 6 × 50 = 300 MHz —
                //     matches Intel's ARL documentation that D2D "can
                //     only be set at boot and cannot be changed in
                //     the operating system".
                return new PlatformOffsets(
                    ngu: new ClockField(0x82F8, 34, 41, 50),
                    d2d: new ClockField(0x82F8, 50, 57, 50),
                    validated: true);

            case IntelOobmsm.Platform.Arl:
            case IntelOobmsm.Platform.Wcl:
            case IntelOobmsm.Platform.Nvl:
            default:
                return default;
        }
    }
}
