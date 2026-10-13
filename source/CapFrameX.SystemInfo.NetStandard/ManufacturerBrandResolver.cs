using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.SystemInfo.NetStandard
{
    /// <summary>
    /// Maps raw manufacturer strings onto the brand people actually use. WMI reports vendors as
    /// full legal-entity prose ("ASUSTeK COMPUTER INC."), SPD as JEP106 names ("G Skill Intl"),
    /// and some BIOS implementations as bare JEDEC IDs. Every spelling of a vendor has to land on
    /// the same brand, otherwise it shows up twice once several sources are merged.
    /// </summary>
    public static class ManufacturerBrandResolver
    {
        /// <summary>
        /// Prefix of the reported manufacturer -> brand. Matched case-insensitively and only on a
        /// word boundary, so "ASUS" cannot swallow "ASUSTeK" - that one carries its own entry.
        /// Ordered longest-first at startup, so a more specific prefix always wins.
        /// </summary>
        private static readonly (string Prefix, string Brand)[] Brands =
            new (string Prefix, string Brand)[]
            {
                ("ASUSTeK", "ASUS"),
                ("ASUS", "ASUS"),
                ("Micro-Star International", "MSI"),
                ("Micro-Star", "MSI"),
                ("MICROSTAR", "MSI"),
                ("MSI", "MSI"),
                ("Giga-Byte", "Gigabyte"),
                ("Gigabyte", "Gigabyte"),
                ("ASRock", "ASRock"),
                ("Biostar", "Biostar"),
                // Memory vendors. SMBIOS spells G.SKILL every way but the official one ("G Skill
                // Intl", "G Skill In", ...). The JEP106 names decoded from SPD have to match the
                // brands of JedecManufacturerIds: without an entry, "Group" would be stripped as
                // a legal form ("Team Group Inc" -> "Team"), and "A-DATA" is the former spelling.
                ("G.Skill", "G.SKILL"),
                ("G Skill", "G.SKILL"),
                ("G-Skill", "G.SKILL"),
                ("GSkill", "G.SKILL"),
                ("Team Group", "Team Group"),
                ("A-DATA", "ADATA"),
                ("Elitegroup", "ECS"),
                ("ECS", "ECS"),
                ("Colorful", "Colorful"),
                ("Maxsun", "Maxsun"),
                ("Onda", "Onda"),
                ("SOYO", "SOYO"),
                ("JGINYUE", "JGINYUE"),
                ("HUANANZHI", "HUANANZHI"),
                ("Huanan", "HUANANZHI"),
                ("Machinist", "Machinist"),
                ("Erying", "Erying"),
                ("Minisforum", "Minisforum"),
                ("Sapphire", "Sapphire"),
                ("EVGA", "EVGA"),
                ("Zotac", "ZOTAC"),
                ("XFX", "XFX"),
                ("Hon Hai", "Foxconn"),
                ("Foxconn", "Foxconn"),
                ("Pegatron", "Pegatron"),
                ("Super Micro Computer", "Supermicro"),
                ("Supermicro", "Supermicro"),
                ("Intel", "Intel"),
                ("Advanced Micro Devices", "AMD"),
                ("AMD", "AMD"),
                ("NVIDIA", "NVIDIA"),
                ("Alienware", "Alienware"),
                ("Dell", "Dell"),
                ("Hewlett-Packard", "HP"),
                ("Hewlett Packard", "HP"),
                ("HP", "HP"),
                ("Lenovo", "Lenovo"),
                ("Acer", "Acer"),
                ("Apple", "Apple"),
                ("Microsoft", "Microsoft"),
                ("Samsung", "Samsung"),
                ("Sony", "Sony"),
                ("Toshiba", "Toshiba"),
                ("Fujitsu", "Fujitsu"),
                ("Medion", "Medion"),
                ("NEC", "NEC"),
                ("International Business Machines", "IBM"),
                ("IBM", "IBM"),
                ("Shuttle", "Shuttle"),
                ("Jetway", "Jetway"),
                ("Clevo", "Clevo"),
                ("Tongfang", "Tongfang"),
                ("Schenker", "Schenker"),
                ("TUXEDO", "TUXEDO"),
                ("Framework", "Framework"),
                ("Razer", "Razer"),
                ("Valve", "Valve"),
                ("Gateway", "Gateway"),
                ("Abit", "Abit"),
                ("AOpen", "AOpen"),
                ("DFI", "DFI"),
                ("EPoX", "EPoX"),
                ("First International Computer", "FIC"),
                ("FIC", "FIC"),
                ("LattePanda", "LattePanda")
            }
            .OrderByDescending(entry => entry.Prefix.Length)
            .ToArray();

        /// <summary>
        /// Some BIOS implementations expose the JEDEC ID instead of the module vendor name.
        /// Best-effort map of the codes common on consumer DDR4/DDR5.
        /// </summary>
        private static readonly Dictionary<string, string> JedecManufacturerIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "802C", "Micron" },
                { "2C00", "Micron" },
                { "80AD", "SK Hynix" },
                { "AD00", "SK Hynix" },
                { "80CE", "Samsung" },
                { "CE00", "Samsung" },
                { "859B", "Crucial" },
                { "029E", "Corsair" },
                { "04CB", "ADATA" },
                { "04CD", "G.SKILL" },
                { "04EF", "Team Group" },
                { "0198", "Kingston" },
                { "7F98", "Kingston" },
            };

        /// <summary>
        /// Values boards and modules ship with when the vendor never filled the DMI fields in.
        /// Compared with dots removed, so "To be filled by O.E.M." and "To be filled by OEM" both
        /// land here.
        /// </summary>
        private static readonly HashSet<string> Placeholders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "to be filled by oem", "tobefilledbyoem", "default string", "oem",
                "system manufacturer", "system product name", "system version", "system name",
                "base board manufacturer", "base board product name", "base board version",
                "none", "na", "n/a", "null", "unknown", "not applicable", "not specified",
                "empty", "-", "--", "0", "xxxx", "invalid"
            };

        /// <summary>Legal forms trailing the vendor name, compared with dots removed.</summary>
        private static readonly HashSet<string> LegalForms =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "inc", "incorporated", "corp", "corporation", "co", "company", "ltd", "limited",
                "llc", "gmbh", "mbh", "ag", "kg", "kgaa", "ohg", "sa", "spa", "bv", "nv", "plc",
                "sro", "pty", "pte", "sdn", "bhd", "group", "holding", "holdings"
            };

        /// <summary>
        /// Filler words trailing an unmapped vendor name. Only used when no brand matched, so a
        /// mapped vendor like "First International Computer" cannot be mangled by them.
        /// </summary>
        private static readonly HashSet<string> GenericDescriptors =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "computer", "computers", "technology", "technologies", "electronics", "electronic",
                "international", "industrial", "industries", "systems", "system", "solutions",
                "manufacturing", "digital"
            };

        /// <summary>
        /// Maps a raw manufacturer string onto its brand. Unknown vendors keep their name with the
        /// legal form and trailing filler words removed.
        /// </summary>
        public static string ToBrand(string manufacturer)
        {
            string vendor = CleanUp(manufacturer);
            if (vendor.Length == 0)
                return string.Empty;

            foreach (var entry in Brands)
            {
                if (StartsWithWord(vendor, entry.Prefix))
                    return entry.Brand;
            }

            return StripBoilerplate(vendor);
        }

        /// <summary>
        /// Maps a memory module vendor, given as a name or as its JEDEC ID, onto its brand.
        /// Returns an empty string when the value is unusable, including JEDEC IDs missing from
        /// the table.
        /// </summary>
        public static string ToMemoryBrand(string rawManufacturer)
        {
            var raw = rawManufacturer?.Trim() ?? string.Empty;
            if (JedecManufacturerIds.TryGetValue(raw, out var mapped))
                return mapped;

            // All-letter strings ("ADATA") are names, not IDs.
            if (raw.Length == 4 && raw.All(Uri.IsHexDigit) && raw.Any(char.IsDigit))
                return string.Empty;

            return ToBrand(raw);
        }

        /// <summary>
        /// Prefers the module vendors read from SPD. Until every installed module has a usable
        /// SPD identity, retain WMI vendors too so partial detection cannot hide mixed memory.
        /// </summary>
        public static string ResolveMemoryManufacturer(IEnumerable<string> spdManufacturers, string wmiManufacturer, int wmiModuleCount)
        {
            var spd = (spdManufacturers ?? Enumerable.Empty<string>())
                .Select(ToMemoryBrand)
                .Where(brand => brand.Length > 0)
                .ToList();

            IEnumerable<string> brands = spd;
            if (spd.Count == 0 || spd.Count < wmiModuleCount)
            {
                var wmi = (wmiManufacturer ?? string.Empty)
                    .Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(ToMemoryBrand)
                    .Where(brand => brand.Length > 0);
                brands = brands.Concat(wmi);
            }

            return string.Join(" / ", brands.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Collapses whitespace, removes commas and returns an empty string for placeholders.
        /// </summary>
        internal static string CleanUp(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // The record header keeps names in comma-separated fields, so commas have to go
            // regardless of how much else is trimmed.
            string collapsed = CollapseWhitespace(value.Replace(',', ' '));

            return IsPlaceholder(collapsed) ? string.Empty : collapsed;
        }

        /// <summary>True when <paramref name="value"/> starts with the whole word <paramref name="prefix"/>.</summary>
        internal static bool StartsWithWord(string value, string prefix)
        {
            if (prefix.Length == 0 || value.Length < prefix.Length)
                return false;

            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            // A prefix only counts when the vendor name really ends there.
            return value.Length == prefix.Length || !char.IsLetterOrDigit(value[prefix.Length]);
        }

        private static bool IsPlaceholder(string value)
        {
            string key = value.Replace(".", string.Empty).Trim();
            return key.Length == 0 || Placeholders.Contains(key);
        }

        private static string StripBoilerplate(string vendor)
        {
            var tokens = vendor.Split(' ').ToList();

            // Never strip a vendor down to nothing - the leading token is the name itself.
            while (tokens.Count > 1)
            {
                string key = tokens[tokens.Count - 1].Replace(".", string.Empty);
                if (!LegalForms.Contains(key) && !GenericDescriptors.Contains(key))
                    break;

                tokens.RemoveAt(tokens.Count - 1);
            }

            return string.Join(" ", tokens);
        }

        private static string CollapseWhitespace(string value)
            => string.Join(" ", value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }
}
