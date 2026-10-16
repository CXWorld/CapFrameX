using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CapFrameX.Test.Localization
{
    /// <summary>
    /// RtssTextFormatter keeps each value format's translation instead of splitting the format
    /// with a regex for every entry on every refresh. The output must stay what the uncached
    /// translation produced.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // switches the process-wide overlay language
    public class RtssTextFormatterCacheTest
    {
        private const string PowerFormat = "<S2><C3>{0,6:F1}<C><S><S0>W<C><S>";
        private string _overlayLanguage;

        [TestInitialize]
        public void Initialize()
        {
            _overlayLanguage = CxLang.Instance.OverlayLanguage;
            CxLang.Instance.SetOverlayLanguage("en");
            RtssTextFormatter.ClearCachedFormats();
        }

        [TestCleanup]
        public void Cleanup()
        {
            CxLang.Instance.SetOverlayLanguage(_overlayLanguage);
            RtssTextFormatter.ClearCachedFormats();
        }

        [TestMethod]
        public void RepeatedRefreshes_TranslateEachFormatOnce()
        {
            CxLang.Instance.SetOverlayLanguage("ru");

            for (int refresh = 0; refresh < 100; refresh++)
            {
                Format("CpuPower", refresh * 1.5, PowerFormat, 1251);
                Format("Frametime", refresh * 0.5, "<C3>{0:F1}<C> ms", 1251);
            }

            Assert.AreEqual(2, RtssTextFormatter.CachedFormatCount);
        }

        [TestMethod]
        public void Values_AreFormattedOnEveryCall()
        {
            // Only the format is kept, never a formatted value.
            Assert.AreEqual("<C3>12.0<C> ms", Format("Frametime", 12.0, "<C3>{0:F1}<C> ms", 65001));
            Assert.AreEqual("<C3>7.5<C> ms", Format("Frametime", 7.5, "<C3>{0:F1}<C> ms", 65001));
            Assert.AreEqual(1, RtssTextFormatter.CachedFormatCount);
        }

        [TestMethod]
        public void CodePages_KeepTheirOwnTranslation()
        {
            CxLang.Instance.SetOverlayLanguage("ru");
            string watt = CxLang.Instance.TranslateOverlay("W");
            Assert.AreNotEqual("W", watt, "The test needs a unit that Russian translates.");

            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>" + watt + "<C><S>", Format("CpuPower", 12.34, PowerFormat, 1251));
            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>W<C><S>", Format("CpuPower", 12.34, PowerFormat, 1252),
                "A Western code page cannot show Cyrillic and keeps the English unit.");
            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>" + watt + "<C><S>", Format("CpuPower", 12.34, PowerFormat, 1251));
        }

        [TestMethod]
        public void OverlayLanguageSwitch_UsesTheNewLanguageAtOnce()
        {
            CxLang.Instance.SetOverlayLanguage("ru");
            string watt = CxLang.Instance.TranslateOverlay("W");
            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>" + watt + "<C><S>", Format("CpuPower", 12.34, PowerFormat, 65001));

            CxLang.Instance.SetOverlayLanguage("en");
            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>W<C><S>", Format("CpuPower", 12.34, PowerFormat, 65001));

            CxLang.Instance.SetOverlayLanguage("ru");
            Assert.AreEqual("<S2><C3>  12.3<C><S><S0>" + watt + "<C><S>", Format("CpuPower", 12.34, PowerFormat, 65001));
        }

        [TestMethod]
        [DataRow("en")]
        [DataRow("de")]
        [DataRow("es")]
        [DataRow("ru")]
        public void CachedTranslation_MatchesTheUncachedOne(string language)
        {
            CxLang.Instance.SetOverlayLanguage(language);
            string[] formats =
            {
                PowerFormat,
                "<C3>{0:F1}<C> ms",
                "<S1><C=AEEA00>{0:F0}<C><S> FPS",
                "{0} Temperature",
                "<C250>Load {0:F0} %<C>",
                "no placeholder at all",
            };

            foreach (int codePage in new[] { 1251, 1252, 65001 })
            {
                foreach (string format in formats)
                {
                    string expected = string.Format(CultureInfo.InvariantCulture,
                        UncachedFormat(format, codePage), 42.25);
                    Assert.AreEqual(expected, Format("Sensor", 42.25, format, codePage), $"{format} ({codePage}, first call)");
                    Assert.AreEqual(expected, Format("Sensor", 42.25, format, codePage), $"{format} ({codePage}, cached)");
                }
            }
        }

        [TestMethod]
        public void Cache_StaysBounded()
        {
            for (int i = 0; i < RtssTextFormatter.MaxCachedFormats + 10; i++)
                Format("Sensor", i, "<C" + i + ">{0}<C>", 65001);

            Assert.IsTrue(RtssTextFormatter.CachedFormatCount <= RtssTextFormatter.MaxCachedFormats,
                $"{RtssTextFormatter.CachedFormatCount} formats cached");
        }

        // The translation as FormatValue did it before the cache.
        private static string UncachedFormat(string format, int codePage)
            => Regex.Replace(format, @"(<[^>]*>|\{[^}]*\})|([^<{]+)", part => part.Groups[2].Success
                ? RtssTextFormatter.Translate(part.Value, codePage) : part.Value);

        private static string Format(string identifier, object value, string valueFormat, int codePage)
        {
            using var entry = new OverlayEntryWrapper(identifier) { Value = value, ValueFormat = valueFormat };
            return RtssTextFormatter.FormatValue(entry, codePage);
        }
    }
}
