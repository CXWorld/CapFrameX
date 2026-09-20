using CapFrameX.Contracts.Overlay;
using System;

namespace CapFrameX.ViewModel.SubModels
{
    /// <summary>
    /// Presentation model for a CPU-Z style vendor mark: a rounded chip carrying the
    /// brand wordmark in the vendor's corporate colors. Rendered as vector text, so it
    /// stays crisp in both themes without shipping trademarked logo artwork.
    /// </summary>
    public class VendorBadge
    {
        public string Text { get; }

        public string Background { get; }

        public string Foreground { get; }

        public VendorBadge(string text, string background, string foreground = "White")
        {
            Text = text;
            Background = background;
            Foreground = foreground;
        }

        public static VendorBadge FromCpuVendor(ECpuVendor vendor)
        {
            switch (vendor)
            {
                case ECpuVendor.Intel:
                    return Intel;
                case ECpuVendor.Amd:
                    return Amd;
                default:
                    return null;
            }
        }

        public static VendorBadge FromGpuVendor(EGpuVendor vendor)
        {
            switch (vendor)
            {
                case EGpuVendor.Nvidia:
                    return new VendorBadge("NVIDIA", "#76B900");
                case EGpuVendor.Amd:
                    return Amd;
                case EGpuVendor.Intel:
                    return Intel;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Maps a free-form vendor string (mainboard or RAM manufacturer) onto a badge.
        /// Unknown but non-empty vendors get a neutral chip with their own name.
        /// </summary>
        public static VendorBadge FromVendorName(string vendorName)
        {
            if (string.IsNullOrWhiteSpace(vendorName))
                return null;

            foreach (var (key, badge) in KnownVendors)
            {
                if (vendorName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return badge;
            }

            var text = vendorName.Trim();
            if (text.Length > 10)
                text = text.Substring(0, 10);

            return new VendorBadge(text, "#5A6472");
        }

        private static VendorBadge Intel => new VendorBadge("intel", "#0068B5");

        private static VendorBadge Amd => new VendorBadge("AMD", "#ED1C24");

        private static readonly (string Key, VendorBadge Badge)[] KnownVendors =
        {
            ("NVIDIA", new VendorBadge("NVIDIA", "#76B900")),
            ("Intel", new VendorBadge("intel", "#0068B5")),
            ("AMD", new VendorBadge("AMD", "#ED1C24")),
            ("ASUS", new VendorBadge("ASUS", "#00539B")),
            ("MSI", new VendorBadge("msi", "#E4002B")),
            ("Gigabyte", new VendorBadge("GIGABYTE", "#E76F00")),
            ("ASRock", new VendorBadge("ASRock", "#0E8B46")),
            ("Biostar", new VendorBadge("BIOSTAR", "#D01F26")),
            ("EVGA", new VendorBadge("EVGA", "#3A3A3A")),
            ("NZXT", new VendorBadge("NZXT", "#7D55C7")),
            ("Corsair", new VendorBadge("CORSAIR", "#FFD200", "#1A1A1A")),
            ("G.Skill", new VendorBadge("G.SKILL", "#B01319")),
            ("Kingston", new VendorBadge("Kingston", "#C8102E")),
            ("Crucial", new VendorBadge("crucial", "#2E7BBF")),
            ("Micron", new VendorBadge("Micron", "#0033A0")),
            ("Samsung", new VendorBadge("SAMSUNG", "#1428A0")),
            ("SK Hynix", new VendorBadge("SK hynix", "#EA5B26")),
            ("Hynix", new VendorBadge("SK hynix", "#EA5B26")),
            ("ADATA", new VendorBadge("ADATA", "#E4002B")),
            ("XPG", new VendorBadge("XPG", "#E4002B")),
            ("Team Group", new VendorBadge("TEAMGROUP", "#D6001C")),
            ("TeamGroup", new VendorBadge("TEAMGROUP", "#D6001C")),
            ("Patriot", new VendorBadge("PATRIOT", "#C41230")),
        };
    }
}
