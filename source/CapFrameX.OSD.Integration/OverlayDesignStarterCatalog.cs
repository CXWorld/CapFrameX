using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayDesignStarterProfile
    {
        internal OverlayDesignStarterProfile(string presetId, string json)
        {
            PresetId = presetId;
            Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("CapFrameX.TileOverlay.Starter/" + presetId)))
                .Substring(0, 32).ToLowerInvariant();
            DesignJson = OverlayRuntimeDesign.Canonicalize(json);
            Name = (string)JObject.Parse(DesignJson)["name"];
        }

        public string PresetId { get; }
        public string Id { get; }
        public string Name { get; }
        public string DesignJson { get; }
    }

    /// <summary>
    /// Ready-to-use saved tile designs available before the WPF designer opens. Embedded
    /// canonical scenes keep startup independent of the optional Controls assembly.
    /// The catalog is installed once; deleting or editing a starter is a normal user action.
    /// </summary>
    public static class OverlayDesignStarterCatalog
    {
        public const int Version = 1;
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
                "benchmark", "compact", "minimal", "frame-pacing", "gpu-dashboard", "memory-watch",
                "vertical-thread-strip", "vertical-clock-deck", "vertical-thermal-panel",
                "hybrid-benchmark", "hybrid-compact", "hybrid-thread-deck"
            }.Select(id =>
            {
                using var stream = assembly.GetManifestResourceStream("CapFrameX.OSD.Integration.StarterDesigns." + id + ".json")
                    ?? throw new InvalidOperationException("A built-in tile overlay design is missing: " + id);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return new OverlayDesignStarterProfile(id, reader.ReadToEnd());
            }).ToArray());
        }
    }
}
