using System;

namespace CapFrameX.SystemInfo.NetStandard
{
    /// <summary>
    /// WMI reports <c>Win32_BaseBoard</c> as full legal-entity prose, so a board name arrives as
    /// "ASUSTeK COMPUTER INC. ROG MAXIMUS XI HERO" - far too long for an overlay line or a record
    /// header. This maps the vendor part onto its brand (<see cref="ManufacturerBrandResolver"/>),
    /// drops the placeholder strings boards are shipped with, and keeps the model untouched.
    /// </summary>
    public static class MainboardNameShortener
    {
        /// <summary>
        /// Builds the display name from the raw <c>Win32_BaseBoard</c> manufacturer and product.
        /// Returns an empty string when neither carries usable information.
        /// </summary>
        public static string Shorten(string manufacturer, string product)
        {
            string board = ManufacturerBrandResolver.CleanUp(product);
            string brand = ManufacturerBrandResolver.ToBrand(manufacturer);

            if (board.Length == 0)
                return brand;

            if (brand.Length == 0)
                return board;

            // Boards that already carry their brand ("ASRock B650M Pro RS") must not get it twice.
            if (ManufacturerBrandResolver.StartsWithWord(board, brand))
                return board;

            // Same thing spelled the long way ("ASUSTeK ROG ..." next to brand "ASUS"): replace the
            // vendor token instead of prefixing a second one.
            int firstSpace = board.IndexOf(' ');
            if (firstSpace > 0)
            {
                string firstToken = board.Substring(0, firstSpace);
                if (string.Equals(ManufacturerBrandResolver.ToBrand(firstToken), brand, StringComparison.OrdinalIgnoreCase))
                    return brand + " " + board.Substring(firstSpace + 1);
            }

            return brand + " " + board;
        }
    }
}
