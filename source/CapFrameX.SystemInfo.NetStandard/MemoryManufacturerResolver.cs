using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.SystemInfo.NetStandard
{
    public static class MemoryManufacturerResolver
    {
        // Some BIOS implementations expose the JEDEC ID instead of the module vendor name.
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

        public static string Normalize(string rawManufacturer)
        {
            var raw = rawManufacturer?.Trim() ?? string.Empty;
            if (JedecManufacturerIds.TryGetValue(raw, out var mapped))
                return mapped;

            if (raw.Length == 4 && raw.All(Uri.IsHexDigit) && raw.Any(char.IsDigit))
                return string.Empty;

            return MainboardNameShortener.ToBrand(raw);
        }

        /// <summary>
        /// Prefers the module vendors read from SPD. Until every installed module has a usable
        /// SPD identity, retain WMI vendors too so partial detection cannot hide mixed memory.
        /// </summary>
        public static string Resolve(IEnumerable<string> spdManufacturers, string wmiManufacturer, int wmiModuleCount)
        {
            var spd = (spdManufacturers ?? Enumerable.Empty<string>())
                .Select(Normalize)
                .Where(brand => brand.Length > 0)
                .ToList();

            IEnumerable<string> brands = spd;
            if (spd.Count == 0 || spd.Count < wmiModuleCount)
            {
                var wmi = (wmiManufacturer ?? string.Empty)
                    .Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Normalize)
                    .Where(brand => brand.Length > 0);
                brands = brands.Concat(wmi);
            }

            return string.Join(" / ", brands.Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
