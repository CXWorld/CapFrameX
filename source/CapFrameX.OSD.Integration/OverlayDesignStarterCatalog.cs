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
        private readonly string _previousDesignHash;

        internal OverlayDesignStarterProfile(string presetId, string json, int introducedInVersion = 1, string previousDesignHash = null)
        {
            PresetId = presetId;
            IntroducedInVersion = introducedInVersion;
            Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("CapFrameX.TileOverlay.Starter/" + presetId)))
                .Substring(0, 32).ToLowerInvariant();
            DesignJson = OverlayRuntimeDesign.Canonicalize(json);
            Name = (string)JObject.Parse(DesignJson)["name"];
            _previousDesignHash = previousDesignHash;
        }

        public string PresetId { get; }
        public string Id { get; }
        public string Name { get; }
        public string DesignJson { get; }
        internal int IntroducedInVersion { get; }

        internal bool CanUpdate(OverlayDesignProfile profile) => _previousDesignHash != null
            && profile.Id == Id && profile.Name == Name
            && ComputeDesignHash(profile.DesignJson) == _previousDesignHash;

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
        public const int Version = 8;
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
                // Exact fingerprints of the version-four exports before the REDLINE header
                // and Ryzen badge corrections, of the FUSION export (versions four and five)
                // before its per-core-type rework, and of the version-seven GREENLINE and ICELINE
                // exports whose framerate readouts adopted the bound "FPS" unit. Never replace a
                // renamed or edited profile.
                string previousDesignHash = entry.Id switch
                {
                    "AMD-Ryzen-Radeon-REDLINE" => "51A4346D1187B02E4FFE6DA2ECB56F28ACDBEED92F4FEE8B29B44C40BF2F5FBD",
                    "RTX-5090-Core-Ultra-285K-FUSION" => "CD0BF73BAE3EDC41CDC305D6919B38580040DC3B160E6BE80088045F7E93BB21",
                    "AMD-Ryzen-5-9600X-Badge" => "09795802837F766CB0350464E82B878EA4A361141F8FEC6CDB9343A314446E40",
                    "AMD-Ryzen-7-9800X3D-Badge" => "2B9D493E89D1A3CAFAE4B4CED21BF25D85BF3025FDA82E96893A24BD09A204A0",
                    "NVIDIA-GeForce-RTX-GREENLINE" => "81B0EB48109F19427A3B7A9DC54883EAC83ACAEDEDDC3D7E64BFD35F5E6DDF17",
                    "ICELINE-Vertical" => "2236C31C8D68F5CDBA90CFBE894398DA0A23B08D48D0760C1DAB6E7516F7E2C5",
                    _ => null
                };
                return new OverlayDesignStarterProfile(entry.Id, reader.ReadToEnd(), entry.Version, previousDesignHash);
            }).ToArray());
        }
    }
}
