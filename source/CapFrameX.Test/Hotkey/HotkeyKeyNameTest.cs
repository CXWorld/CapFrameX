using System.Windows.Forms;
using CapFrameX.Hotkey;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Hotkey
{
    [TestClass]
    public class HotkeyKeyNameTest
    {
        /// <summary>
        /// The regression that motivated <see cref="HotkeyKeyName"/>: a capture hotkey stored as
        /// "OemBackslash" by a .NET Framework build stopped firing after the SDK-style .NET
        /// migration, because the hook now reports the same virtual key as "Oem102".
        /// </summary>
        [TestMethod]
        public void Canonicalize_AliasNamesMatchWhatTheHookReports()
        {
            // Both names are the same virtual key (226) — whichever one ToString returns on this
            // framework, canonicalizing either name has to produce exactly that.
            string reported = ((Keys)226).ToString();

            Assert.AreEqual(reported, HotkeyKeyName.Canonicalize("OemBackslash"));
            Assert.AreEqual(reported, HotkeyKeyName.Canonicalize("Oem102"));
        }

        [TestMethod]
        public void Canonicalize_CoversEveryKeyTheTwoFrameworksNameDifferently()
        {
            // The full set of virtual keys whose canonical Keys name changed between
            // .NET Framework 4.7.2 and modern .NET, with both spellings as they occur in stored
            // configurations. Enter/Return (13) matters as much as the OEM block: it is a
            // plausible capture hotkey.
            var aliasPairs = new[]
            {
                (13, "Return", "Enter"),
                (25, "HanjaMode", "KanjiMode"),
                (186, "Oem1", "OemSemicolon"),
                (191, "OemQuestion", "Oem2"),
                (219, "OemOpenBrackets", "Oem4"),
                (220, "Oem5", "OemPipe"),
                (221, "Oem6", "OemCloseBrackets"),
                (222, "Oem7", "OemQuotes"),
                (226, "OemBackslash", "Oem102")
            };

            foreach (var (virtualKey, legacyName, modernName) in aliasPairs)
            {
                string reported = ((Keys)virtualKey).ToString();
                Assert.AreEqual(reported, HotkeyKeyName.Canonicalize(legacyName),
                    $"VK {virtualKey}: '{legacyName}' must canonicalize to the reported name");
                Assert.AreEqual(reported, HotkeyKeyName.Canonicalize(modernName),
                    $"VK {virtualKey}: '{modernName}' must canonicalize to the reported name");
            }
        }

        [TestMethod]
        public void Canonicalize_LeavesUnambiguousNamesAlone()
        {
            // Letter and function keys have a single name and must not be rewritten — the
            // overlay hotkeys (Alt+O, Alt+R, ...) rely on this and were never affected.
            Assert.AreEqual("A", HotkeyKeyName.Canonicalize("A"));
            Assert.AreEqual("F10", HotkeyKeyName.Canonicalize("F10"));
            Assert.AreEqual("NumPad5", HotkeyKeyName.Canonicalize("NumPad5"));
        }

        [TestMethod]
        public void Canonicalize_PassesThroughWhatItCannotResolve()
        {
            // An unknown name cannot match a hook event either way; keeping it verbatim leaves
            // the configured value visible in diagnostics instead of masking it.
            Assert.AreEqual("NotAKey", HotkeyKeyName.Canonicalize("NotAKey"));
            Assert.AreEqual("99999", HotkeyKeyName.Canonicalize("99999"));
            Assert.AreEqual(string.Empty, HotkeyKeyName.Canonicalize(string.Empty));
            Assert.IsNull(HotkeyKeyName.Canonicalize(null));
        }
    }
}
