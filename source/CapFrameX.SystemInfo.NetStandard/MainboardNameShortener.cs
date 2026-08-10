using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.SystemInfo.NetStandard
{
    /// <summary>
    /// WMI reports <c>Win32_BaseBoard</c> as full legal-entity prose, so a board name arrives as
    /// "ASUSTeK COMPUTER INC. ROG MAXIMUS XI HERO" - far too long for an overlay line or a record
    /// header. This maps the vendor part onto the brand people actually use, drops the placeholder
    /// strings boards are shipped with, and keeps the model untouched.
    /// </summary>
    public static class MainboardNameShortener
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
                // Memory vendors reach ToBrand through the RAM manufacturer field, where SMBIOS
                // spells G.SKILL every way but the official one ("G Skill Intl", "G Skill In", ...).
                ("G.Skill", "G.SKILL"),
                ("G Skill", "G.SKILL"),
                ("G-Skill", "G.SKILL"),
                ("GSkill", "G.SKILL"),
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
        /// Values boards ship with when the vendor never filled the DMI fields in. Compared with
        /// dots removed, so "To be filled by O.E.M." and "To be filled by OEM" both land here.
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
        /// Builds the display name from the raw <c>Win32_BaseBoard</c> manufacturer and product.
        /// Returns an empty string when neither carries usable information.
        /// </summary>
        public static string Shorten(string manufacturer, string product)
        {
            string vendor = Normalize(manufacturer);
            string board = Normalize(product);
            string brand = ToBrand(vendor);

            if (board.Length == 0)
                return brand;

            if (brand.Length == 0)
                return board;

            // Boards that already carry their brand ("ASRock B650M Pro RS") must not get it twice.
            if (StartsWithWord(board, brand))
                return board;

            // Same thing spelled the long way ("ASUSTeK ROG ..." next to brand "ASUS"): replace the
            // vendor token instead of prefixing a second one.
            int firstSpace = board.IndexOf(' ');
            if (firstSpace > 0)
            {
                string firstToken = board.Substring(0, firstSpace);
                if (string.Equals(ToBrand(firstToken), brand, StringComparison.OrdinalIgnoreCase))
                    return brand + " " + board.Substring(firstSpace + 1);
            }

            return brand + " " + board;
        }

        /// <summary>
        /// Maps a raw manufacturer string onto its brand. Unknown vendors keep their name with the
        /// legal form and trailing filler words removed.
        /// </summary>
        public static string ToBrand(string manufacturer)
        {
            string vendor = Normalize(manufacturer);
            if (vendor.Length == 0)
                return string.Empty;

            foreach (var entry in Brands)
            {
                if (StartsWithWord(vendor, entry.Prefix))
                    return entry.Brand;
            }

            return StripBoilerplate(vendor);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // The record header keeps the board name in a single comma-separated field, so commas
            // have to go regardless of how much else is trimmed.
            string collapsed = CollapseWhitespace(value.Replace(',', ' '));

            return IsPlaceholder(collapsed) ? string.Empty : collapsed;
        }

        private static bool IsPlaceholder(string value)
        {
            string key = value.Replace(".", string.Empty).Trim();
            return key.Length == 0 || Placeholders.Contains(key);
        }

        private static bool StartsWithWord(string value, string prefix)
        {
            if (prefix.Length == 0 || value.Length < prefix.Length)
                return false;

            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            // A prefix only counts when the vendor name really ends there.
            return value.Length == prefix.Length || !char.IsLetterOrDigit(value[prefix.Length]);
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
