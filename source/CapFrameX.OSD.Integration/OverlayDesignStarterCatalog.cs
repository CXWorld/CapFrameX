using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayDesignStarterProfile
    {
        private readonly string[] _previousDesignHashes;

        internal OverlayDesignStarterProfile(string presetId, string json, int introducedInVersion = 1,
            params string[] previousDesignHashes)
            : this(presetId, presetId, json, introducedInVersion, previousDesignHashes)
        {
        }

        /// <summary>A renamed preset keeps the ID of its former name, so installed copies stay the same profile.</summary>
        internal OverlayDesignStarterProfile(string presetId, string idPresetId, string json, int introducedInVersion,
            string[] previousDesignHashes)
        {
            PresetId = presetId;
            IntroducedInVersion = introducedInVersion;
            Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("CapFrameX.TileOverlay.Starter/" + idPresetId)))
                .Substring(0, 32).ToLowerInvariant();
            DesignJson = OverlayRuntimeDesign.Canonicalize(json);
            Name = (string)JObject.Parse(DesignJson)["name"];
            _previousDesignHashes = previousDesignHashes ?? Array.Empty<string>();
        }

        public string PresetId { get; }
        public string Id { get; }
        public string Name { get; }
        public string DesignJson { get; }
        /// <summary>What the design shows and whom it credits; empty for CapFrameX's own designs.</summary>
        public IReadOnlyList<LocalizedText> Description { get; internal init; } = Array.Empty<LocalizedText>();
        /// <summary>Web pages of the authors whose work the design is adapted from.</summary>
        public IReadOnlyList<OverlayDesignLink> Links { get; internal init; } = Array.Empty<OverlayDesignLink>();
        /// <summary>Names the shipped design carried before a catalog upgrade renamed it.</summary>
        internal IReadOnlyList<string> PreviousNames { get; init; } = Array.Empty<string>();
        internal int IntroducedInVersion { get; }

        internal bool CanUpdate(OverlayDesignProfile profile) => _previousDesignHashes.Length > 0
            && profile.Id == Id && (profile.Name == Name || PreviousNames.Contains(profile.Name, StringComparer.Ordinal))
            && _previousDesignHashes.Contains(ComputeDesignHash(profile.DesignJson), StringComparer.Ordinal);

        internal static string ComputeDesignHash(string json) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(OverlayRuntimeDesign.Canonicalize(json))));
    }

    /// <summary>
    /// Ready-to-use saved tile designs available before the WPF designer opens. Embedded
    /// canonical scenes keep startup independent of the optional Controls assembly.
    /// Catalog upgrades can correct known unmodified revisions; user edits and deletions are retained.
    /// </summary>
    public static class OverlayDesignStarterCatalog
    {
        public const int Version = 22;
        private static readonly Lazy<IReadOnlyList<OverlayDesignStarterProfile>> Catalog = new Lazy<IReadOnlyList<OverlayDesignStarterProfile>>(Load);
        public static IReadOnlyList<OverlayDesignStarterProfile> Profiles => Catalog.Value;

        public static bool EnsureProfiles(OverlayDesignProfileStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            return store.EnsureStarterProfiles(Profiles, Version);
        }

        /// <summary>
        /// A saved profile as the profile lists show it. A profile that started as a shipped design keeps
        /// that design's description and credits through renames and edits; a copy is the user's own design.
        /// </summary>
        public static OverlayDesignInfo Describe(OverlayDesignProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var starter = Profiles.FirstOrDefault(item => item.Id == profile.Id);
            return new OverlayDesignInfo(profile.Id, profile.Name, starter?.Description, starter?.Links);
        }

        public static bool IsUnmodifiedStarter(OverlayDesignProfile profile)
        {
            if (profile == null) return false;
            var starter = Profiles.FirstOrDefault(item => item.Id == profile.Id);
            return starter != null && profile.Name == starter.Name
                && JToken.DeepEquals(JToken.Parse(profile.DesignJson), JToken.Parse(starter.DesignJson));
        }

        private static IReadOnlyList<OverlayDesignStarterProfile> Load()
        {
            var assembly = typeof(OverlayDesignStarterCatalog).Assembly;
            // The order of the profile lists, grouped by theme. Every catalog upgrade puts the shipped
            // designs of a library into this order, whatever version added them
            // (OverlayDesignProfileStore.EnsureStarterProfiles).
            return Array.AsReadOnly(new[]
            {
                // Everyday layouts
                (Id: "benchmark", Version: 1), (Id: "compact", Version: 1), (Id: "minimal", Version: 1),
                (Id: "OLED-Minimal", Version: 6),
                (Id: "frame-pacing", Version: 1), (Id: "gpu-dashboard", Version: 1), (Id: "memory-watch", Version: 1),
                // Vertical strips
                (Id: "vertical-thread-strip", Version: 1), (Id: "vertical-clock-deck", Version: 1), (Id: "vertical-thermal-panel", Version: 1),
                // Hybrid CPUs and core types
                (Id: "hybrid-benchmark", Version: 1), (Id: "hybrid-compact", Version: 1), (Id: "hybrid-thread-deck", Version: 1),
                (Id: "core-types-overview", Version: 3), (Id: "core-types-compact", Version: 3), (Id: "core-types-thermal", Version: 3),
                // Turquoise theme
                (Id: "turquoise-reference", Version: 2), (Id: "turquoise-compact", Version: 2), (Id: "turquoise-wide", Version: 2),
                // Hardware designs
                (Id: "RTX-50-Ryzen-3D-APEX", Version: 4),
                (Id: "AMD-Ryzen-Radeon-REDLINE", Version: 4),
                (Id: "RTX-50-Core-Ultra-FUSION-ARL", Version: 4),
                (Id: "RTX-50-Core-FUSION-RPL", Version: 18),
                (Id: "RTX-50-Ryzen-FUSION", Version: 18),
                (Id: "NVIDIA-GeForce-RTX-GREENLINE", Version: 7),
                (Id: "ICELINE-Vertical", Version: 7),
                (Id: "Panther-Lake-Arc-Aurora", Version: 6),
                (Id: "AMD-Ryzen-5-9600X-Badge", Version: 4),
                (Id: "AMD-Ryzen-7-9800X3D-Badge", Version: 4),
                // Adapted from other authors
                (Id: "Slick-Overlay-Vertical-Panel", Version: 17),
                (Id: "Slick-Overlay-Wide-3-Rows", Version: 17),
                (Id: "Slick-Overlay-Compact-2x2", Version: 17),
                (Id: "Slick-Overlay-Single-Line", Version: 17)
            }.Select(entry =>
            {
                using var stream = assembly.GetManifestResourceStream("CapFrameX.OSD.Integration.StarterDesigns." + entry.Id + ".json")
                    ?? throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_StarterMissing", entry.Id), message => new InvalidOperationException(message));
                using var reader = new StreamReader(stream, Encoding.UTF8);
                // Exact fingerprints of earlier exports a catalog upgrade replaces: the
                // version-four REDLINE and Ryzen badges before their header and badge corrections,
                // FUSION before its per-core-type rework (versions four and five), the version-seven
                // GREENLINE and ICELINE whose framerate readouts adopted the bound "FPS" unit, and
                // REDLINE, APEX, FUSION and Panther Lake at full scale before version nine reduced
                // their base for resolution-relative sizing, GREENLINE before version ten and ICELINE
                // before version eleven centred the GPU name in their headers, and FUSION before version
                // twelve added its core load heatmap, before version thirteen grouped the heatmap's
                // core types into blocks, before version fourteen labelled them, before version
                // fifteen centred "285K" below its logo and before version sixteen averaged its heatmap
                // over the 10 s default instead of 60 s. Version eighteen made APEX, REDLINE and FUSION
                // (now FUSION ARL) generic: they name the installed CPU and GPU, show no model-specific
                // graphics and bind FUSION's cores on any Core Ultra. Version nineteen doubled the
                // FRAME PACING chart of APEX, REDLINE and the three FUSION designs and gave REDLINE the
                // core load bars and load heatmap of FUSION Ryzen; version twenty moved REDLINE's GPU block
                // above its CPU tiles, as in FUSION. Version twenty-one put the Slick designs' current FPS
                // and hardware names on the baseline of their rows and gave the driver version room.
                // Version twenty-two gave GREENLINE, ICELINE and Turquoise Reference one font, the
                // Bahnschrift their readings and captions had used tile by tile.
                // Never replace a renamed or edited profile.
                string[] previousDesignHashes = entry.Id switch
                {
                    "RTX-50-Ryzen-3D-APEX" => new[]
                    {
                        "02913A2E04FABDD253EE7FC4D37F6FED8BE06A0E89B15E61C03CBDD52AC6D716",
                        "F0B27CF3BF6D64A106E14D4BC52B9DB1B55E5343F780CB5FFFC04033C7B3DB42",
                        "01FE6779D668FB83E0849836368829A234356E40E6B281143B29E9220F0DB4E8"
                    },
                    "AMD-Ryzen-Radeon-REDLINE" => new[]
                    {
                        "51A4346D1187B02E4FFE6DA2ECB56F28ACDBEED92F4FEE8B29B44C40BF2F5FBD",
                        "29F584D14371B202EC0DC2B813EBFB9B3C1BD97E049898EEEF730EB134F75A38",
                        "11BD3AAAC5BC84AF1DB602A2123FBA4417F20DB564780B26A83CC2FB2A5CEA69",
                        "6E33A0794A24181015C63E99E1294A2CA2415000BB053CE278C107A5A6160819",
                        "611C240A718B67887AD76A3D0AA7A4FABE613BBBD07FAE063236BABF6B08FF0E"
                    },
                    "RTX-50-Core-Ultra-FUSION-ARL" => new[]
                    {
                        "CD0BF73BAE3EDC41CDC305D6919B38580040DC3B160E6BE80088045F7E93BB21",
                        "A3F9D621DC0A7E2FCD1AAC0F75EF58B2ADACCD63A38B843761EEAA5CB8BD6AF2",
                        "8C874E3BC06D09F722627AB3978316AA613733D11CBE18337E0C41570DE7D0EF",
                        "338F4F0E7525D2CD5D9B4096612C869DECEE947A3A53204E362304544501E276",
                        "A93FBDED3CDEE2E51EE9D76F12907E2FB0C2CE5629C3175AEC72C0EFD83D8465",
                        "375AD5D0DDF1F923960970412183A0A5A8F5704170BB955235818552608037F0",
                        "2F2081F41702F0156285B055621F0350AC7FC61514600DA6CBF17FFF3BC4B8F4",
                        "D784C960AB557AA56C0CFEFD3D8F04272396B30D34C7B34CA0BD6A541FDE6168",
                        "C6C444013E2983BBB55C1B8ED0ED3C69B704E98807AF745BE4D744EA9414E91A"
                    },
                    "RTX-50-Core-FUSION-RPL" => new[] { "9FC5D1D6A8DA1F2D9D51C07158659A39EA9FF14B72399E2CE403BB2BFB986D46" },
                    "RTX-50-Ryzen-FUSION" => new[] { "9D589B66E0E8FE52B9F46459E792FD1178A561D26254A323BEDD974CB8832BB9" },
                    "Panther-Lake-Arc-Aurora" => new[] { "E717AF2FB7BDDF804AFF1918BA97CC560D251D648134522040A4F7591534C9ED" },
                    "AMD-Ryzen-5-9600X-Badge" => new[] { "09795802837F766CB0350464E82B878EA4A361141F8FEC6CDB9343A314446E40" },
                    "AMD-Ryzen-7-9800X3D-Badge" => new[] { "2B9D493E89D1A3CAFAE4B4CED21BF25D85BF3025FDA82E96893A24BD09A204A0" },
                    "NVIDIA-GeForce-RTX-GREENLINE" => new[]
                    {
                        "81B0EB48109F19427A3B7A9DC54883EAC83ACAEDEDDC3D7E64BFD35F5E6DDF17",
                        "EE44FF48BB637A51B5AB8D870EFDD0A29B2902B918769334722D20D4D767930F",
                        "F7D0B92297C1642956627AA516A0808C6BD5CC5871F6C420BE304540291493BA"
                    },
                    "ICELINE-Vertical" => new[]
                    {
                        "2236C31C8D68F5CDBA90CFBE894398DA0A23B08D48D0760C1DAB6E7516F7E2C5",
                        "15BF2A0A4109D67A91B23A1B8E6AFB2EEADEA4FCCFA79265A6E1B110D0419143",
                        "BD5DB1FD01A7D5D7B5FCE3BC2B6C39CD1B2CF16E399786C1798B3AC3B56A90FD"
                    },
                    "turquoise-reference" => new[] { "8D87F7B59BA05436CA4968439662372DE4D6E14FFE199B1CE87598DDE181EB06" },
                    "Slick-Overlay-Vertical-Panel" => new[] { "9A30110C438204C115A22A0EFDC2D3F4F1758E8E6C4EAEF51283DEEB6B0C52D6" },
                    "Slick-Overlay-Wide-3-Rows" => new[] { "C3CC520265BAD7EA85707F9DFA1D94E27996FE84FDF0F322B423756B019F6C18" },
                    "Slick-Overlay-Compact-2x2" => new[] { "91184A06DADEE4782C7ABEE4E7B691FFAC0AA51E07AE44A6D1156A1ED6527276" },
                    "Slick-Overlay-Single-Line" => new[] { "84B344FF29DC86F8B318FEDEB12C47BF75897B4709659AC288F2482300D0E08A" },
                    _ => Array.Empty<string>()
                };
                // The generic designs replaced presets named after one CPU and GPU.
                var (idPresetId, previousNames) = entry.Id switch
                {
                    "RTX-50-Ryzen-3D-APEX" => ("RTX-5090-9800X3D-APEX", new[] { "APEX - RTX 5090 + 9800X3D" }),
                    "AMD-Ryzen-Radeon-REDLINE" => (entry.Id, new[] { "REDLINE - Ryzen 7 9800X3D + Radeon" }),
                    "RTX-50-Core-Ultra-FUSION-ARL" => ("RTX-5090-Core-Ultra-285K-FUSION", new[] { "FUSION - RTX 5090 + Core Ultra 9 285K" }),
                    _ => (entry.Id, Array.Empty<string>())
                };
                var (description, links) = About(entry.Id);
                return new OverlayDesignStarterProfile(entry.Id, idPresetId, reader.ReadToEnd(), entry.Version, previousDesignHashes)
                {
                    Description = description, Links = links, PreviousNames = previousNames
                };
            }).ToArray());
        }

        // Designs adapted from other authors' work name them and link to the originals.
        private static readonly OverlayDesignLink[] SlickOverlayLinks =
        {
            new OverlayDesignLink(new LocalizedText("OverlayDesignLink_SlickOverlay"),
                new Uri("https://github.com/HeyOkay/Slick-Overlay-for-RTSS")),
            new OverlayDesignLink(new LocalizedText("OverlayDesignLink_SlickOverlayAuthor"),
                new Uri("https://github.com/HeyOkay")),
            new OverlayDesignLink(new LocalizedText("OverlayDesignLink_SlickOverlayLicense"),
                new Uri("https://github.com/HeyOkay/Slick-Overlay-for-RTSS/blob/main/LICENSE"))
        };

        private static (IReadOnlyList<LocalizedText> Description, IReadOnlyList<OverlayDesignLink> Links) About(string presetId)
        {
            LocalizedText layout = presetId switch
            {
                "Slick-Overlay-Vertical-Panel" => new LocalizedText("OverlayDesignStarter_SlickVerticalPanel"),
                "Slick-Overlay-Wide-3-Rows" => new LocalizedText("OverlayDesignStarter_SlickWide3Rows"),
                "Slick-Overlay-Compact-2x2" => new LocalizedText("OverlayDesignStarter_SlickCompact2x2"),
                "Slick-Overlay-Single-Line" => new LocalizedText("OverlayDesignStarter_SlickSingleLine"),
                _ => null
            };
            if (layout == null)
                return (Array.Empty<LocalizedText>(), Array.Empty<OverlayDesignLink>());
            return (new[] { layout, new LocalizedText("OverlayDesignStarter_SlickCredit"), new LocalizedText("OverlayDesignStarter_SlickNotes") },
                SlickOverlayLinks);
        }
    }
}
