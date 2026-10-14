using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CapFrameX.Contracts.Localization;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayDesignStarterProfile
    {
        private readonly string[] _previousDesignHashes;

        internal OverlayDesignStarterProfile(string presetId, string json, int introducedInVersion = 1,
            params string[] previousDesignHashes)
        {
            PresetId = presetId;
            IntroducedInVersion = introducedInVersion;
            Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("CapFrameX.TileOverlay.Starter/" + presetId)))
                .Substring(0, 32).ToLowerInvariant();
            DesignJson = OverlayRuntimeDesign.Canonicalize(json);
            Name = (string)JObject.Parse(DesignJson)["name"];
            _previousDesignHashes = previousDesignHashes ?? Array.Empty<string>();
        }

        public string PresetId { get; }
        public string Id { get; }
        public string Name { get; }
        public string DesignJson { get; }
        internal int IntroducedInVersion { get; }

        internal bool CanUpdate(OverlayDesignProfile profile) => _previousDesignHashes.Length > 0
            && profile.Id == Id && profile.Name == Name
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
        public const int Version = 9;
        private static readonly Lazy<IReadOnlyList<OverlayDesignStarterProfile>> Catalog = new Lazy<IReadOnlyList<OverlayDesignStarterProfile>>(Load);
        public static IReadOnlyList<OverlayDesignStarterProfile> Profiles => Catalog.Value;

        public static bool EnsureProfiles(OverlayDesignProfileStore store)
        {
            ArgumentNullException.ThrowIfNull(store);
            return store.EnsureStarterProfiles(Profiles, Version);
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
            return Array.AsReadOnly(new[]
            {
                (Id: "benchmark", Version: 1), (Id: "compact", Version: 1), (Id: "minimal", Version: 1),
                (Id: "frame-pacing", Version: 1), (Id: "gpu-dashboard", Version: 1), (Id: "memory-watch", Version: 1),
                (Id: "vertical-thread-strip", Version: 1), (Id: "vertical-clock-deck", Version: 1), (Id: "vertical-thermal-panel", Version: 1),
                (Id: "hybrid-benchmark", Version: 1), (Id: "hybrid-compact", Version: 1), (Id: "hybrid-thread-deck", Version: 1),
                (Id: "turquoise-reference", Version: 2), (Id: "turquoise-compact", Version: 2), (Id: "turquoise-wide", Version: 2),
                (Id: "core-types-overview", Version: 3), (Id: "core-types-compact", Version: 3), (Id: "core-types-thermal", Version: 3),
                (Id: "RTX-5090-9800X3D-APEX", Version: 4),
                (Id: "AMD-Ryzen-Radeon-REDLINE", Version: 4),
                (Id: "RTX-5090-Core-Ultra-285K-FUSION", Version: 4),
                (Id: "AMD-Ryzen-5-9600X-Badge", Version: 4),
                (Id: "AMD-Ryzen-7-9800X3D-Badge", Version: 4),
                (Id: "Panther-Lake-Arc-Aurora", Version: 6),
                (Id: "OLED-Minimal", Version: 6),
                (Id: "NVIDIA-GeForce-RTX-GREENLINE", Version: 7),
                (Id: "ICELINE-Vertical", Version: 7)
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
                // their base for resolution-relative sizing. Never replace a renamed or edited profile.
                string[] previousDesignHashes = entry.Id switch
                {
                    "RTX-5090-9800X3D-APEX" => new[] { "02913A2E04FABDD253EE7FC4D37F6FED8BE06A0E89B15E61C03CBDD52AC6D716" },
                    "AMD-Ryzen-Radeon-REDLINE" => new[]
                    {
                        "51A4346D1187B02E4FFE6DA2ECB56F28ACDBEED92F4FEE8B29B44C40BF2F5FBD",
                        "29F584D14371B202EC0DC2B813EBFB9B3C1BD97E049898EEEF730EB134F75A38"
                    },
                    "RTX-5090-Core-Ultra-285K-FUSION" => new[]
                    {
                        "CD0BF73BAE3EDC41CDC305D6919B38580040DC3B160E6BE80088045F7E93BB21",
                        "A3F9D621DC0A7E2FCD1AAC0F75EF58B2ADACCD63A38B843761EEAA5CB8BD6AF2"
                    },
                    "Panther-Lake-Arc-Aurora" => new[] { "E717AF2FB7BDDF804AFF1918BA97CC560D251D648134522040A4F7591534C9ED" },
                    "AMD-Ryzen-5-9600X-Badge" => new[] { "09795802837F766CB0350464E82B878EA4A361141F8FEC6CDB9343A314446E40" },
                    "AMD-Ryzen-7-9800X3D-Badge" => new[] { "2B9D493E89D1A3CAFAE4B4CED21BF25D85BF3025FDA82E96893A24BD09A204A0" },
                    "NVIDIA-GeForce-RTX-GREENLINE" => new[] { "81B0EB48109F19427A3B7A9DC54883EAC83ACAEDEDDC3D7E64BFD35F5E6DDF17" },
                    "ICELINE-Vertical" => new[] { "2236C31C8D68F5CDBA90CFBE894398DA0A23B08D48D0760C1DAB6E7516F7E2C5" },
                    _ => Array.Empty<string>()
                };
                return new OverlayDesignStarterProfile(entry.Id, reader.ReadToEnd(), entry.Version, previousDesignHashes);
            }).ToArray());
        }
    }
}
