using System;
using System.Windows.Forms;

namespace CapFrameX.Hotkey
{
    /// <summary>
    /// Reconciles the two key-name vocabularies a hotkey passes through.
    ///
    /// A hotkey is stored as the name of a WPF <see cref="System.Windows.Input.Key"/> (the
    /// recorder in the views produces it) but matched against the name of a WinForms
    /// <see cref="Keys"/> value, which is what the global hook reports. Several virtual keys
    /// carry two names in both enums — <c>Keys.OemBackslash</c> and <c>Keys.Oem102</c> are both
    /// 226 — and <c>Enum.ToString</c> returns one of them without either enum promising which.
    ///
    /// .NET Framework and .NET 9 disagree for nine of those values, VK 226 and VK 13
    /// (Return/Enter) among them: .NET Framework reported "OemBackslash", .NET 9 reports
    /// "Oem102". Since the WPF side kept its naming, a config written before the
    /// net9.0-windows migration holds a name the hook no longer produces, the string comparison
    /// in <see cref="KeyCombinationExtensions.OnCXCombination"/> never matches, and the hotkey
    /// silently stops firing.
    ///
    /// Parsing accepts every alias, so routing a name through the parsed value yields the single
    /// name the running framework uses. That keeps existing configurations working, needs no
    /// migration, and stays correct if the canonical name ever changes again.
    /// </summary>
    public static class HotkeyKeyName
    {
        /// <summary>
        /// Returns <paramref name="keyName"/> under the name the running framework's
        /// <see cref="Keys"/> enum reports for it. Unrecognized names are returned unchanged:
        /// they cannot match a hook event either way, and rewriting them would only hide the
        /// configured value from diagnostics.
        /// </summary>
        public static string Canonicalize(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
                return keyName;

            return Enum.TryParse(keyName, true, out Keys key) &&
                   Enum.IsDefined(typeof(Keys), key)
                ? key.ToString()
                : keyName;
        }
    }
}
