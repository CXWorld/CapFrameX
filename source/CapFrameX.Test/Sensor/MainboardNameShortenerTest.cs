using CapFrameX.SystemInfo.NetStandard;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class MainboardNameShortenerTest
    {
        [DataTestMethod]
        // The strings in these rows are what Win32_BaseBoard actually reports.
        [DataRow("ASUSTeK COMPUTER INC.", "ROG MAXIMUS XI HERO", "ASUS ROG MAXIMUS XI HERO")]
        [DataRow("ASUSTeK Computer Inc.", "PRIME X570-PRO", "ASUS PRIME X570-PRO")]
        [DataRow("Micro-Star International Co., Ltd.", "MAG B650 TOMAHAWK WIFI", "MSI MAG B650 TOMAHAWK WIFI")]
        [DataRow("MICRO-STAR INTERNATIONAL CO.,LTD", "MPG X870E CARBON WIFI", "MSI MPG X870E CARBON WIFI")]
        [DataRow("MICROSTAR INTERNATIONAL", "Z270 PC MATE", "MSI Z270 PC MATE")]
        [DataRow("Gigabyte Technology Co., Ltd.", "X870E AORUS PRO", "Gigabyte X870E AORUS PRO")]
        [DataRow("Giga-Byte Technology Co., Ltd.", "Z790 AORUS PRO X", "Gigabyte Z790 AORUS PRO X")]
        [DataRow("ASRock", "X870E Taichi", "ASRock X870E Taichi")]
        [DataRow("Hewlett-Packard", "8433", "HP 8433")]
        [DataRow("Super Micro Computer, Inc.", "X11SWN-E", "Supermicro X11SWN-E")]
        [DataRow("Dell Inc.", "0KWVT8", "Dell 0KWVT8")]
        [DataRow("Shuttle Inc.", "FH67", "Shuttle FH67")]
        [DataRow("Pegatron Corporation", "2AC2", "Pegatron 2AC2")]
        [DataRow("Intel Corporation", "NUC12WSBi7", "Intel NUC12WSBi7")]
        [DataRow("ELITEGROUP COMPUTER SYSTEMS CO.,LTD", "H61H2-M2", "ECS H61H2-M2")]
        public void Shorten_KnownVendor_UsesBrand(string manufacturer, string product, string expected)
        {
            Assert.AreEqual(expected, MainboardNameShortener.Shorten(manufacturer, product));
        }

        [TestMethod]
        public void Shorten_ProductAlreadyCarriesBrand_DoesNotRepeatIt()
        {
            Assert.AreEqual("ASRock B650M Pro RS",
                MainboardNameShortener.Shorten("ASRock", "ASRock B650M Pro RS"));
        }

        [TestMethod]
        public void Shorten_ProductCarriesLongVendorSpelling_ReplacesItWithTheBrand()
        {
            Assert.AreEqual("ASUS TUF GAMING B650-PLUS",
                MainboardNameShortener.Shorten("ASUSTeK COMPUTER INC.", "ASUSTeK TUF GAMING B650-PLUS"));
        }

        [DataTestMethod]
        [DataRow("To be filled by O.E.M.", "To be filled by O.E.M.", "")]
        [DataRow("To Be Filled By O.E.M.", "B650M Pro RS", "B650M Pro RS")]
        [DataRow("System manufacturer", "System Product Name", "")]
        [DataRow("Default string", "Default string", "")]
        [DataRow("ASUSTeK COMPUTER INC.", "Base Board Product Name", "ASUS")]
        [DataRow(null, null, "")]
        [DataRow("", "   ", "")]
        public void Shorten_PlaceholderFields_AreDropped(string manufacturer, string product, string expected)
        {
            Assert.AreEqual(expected, MainboardNameShortener.Shorten(manufacturer, product));
        }

        [DataTestMethod]
        // Unmapped vendors keep their name, minus the legal form and trailing filler words.
        [DataRow("Shenzhen Wingtech Electronics Co., Ltd.", "WTM01", "Shenzhen Wingtech WTM01")]
        [DataRow("Contoso GmbH", "Board 1", "Contoso Board 1")]
        [DataRow("Contoso Inc.", "Board 1", "Contoso Board 1")]
        [DataRow("Foobar Technologies Incorporated", "B2", "Foobar B2")]
        [DataRow("Wistron InfoComm Manufacturing Inc.", "09A2h", "Wistron InfoComm 09A2h")]
        // Same shape as "ASUSTeK COMPUTER INC.": legal form and filler word both go, so even a
        // vendor missing from the brand table comes out short.
        [DataRow("ACME COMPUTER INC.", "X1", "ACME X1")]
        [DataRow("Technology", "Board 2", "Technology Board 2")]
        public void Shorten_UnmappedVendor_StripsBoilerplateOnly(string manufacturer, string product, string expected)
        {
            Assert.AreEqual(expected, MainboardNameShortener.Shorten(manufacturer, product));
        }

        [TestMethod]
        public void Shorten_VendorIsNothingButALegalForm_IsKept()
        {
            // The stripper always leaves the leading token standing, so a vendor can never be
            // stripped out of existence. No real board reports this, it just pins the guard.
            Assert.AreEqual("Inc. Board 3", MainboardNameShortener.Shorten("Inc.", "Board 3"));
        }

        [TestMethod]
        public void Shorten_Result_NeverContainsCommas()
        {
            // The record header stores the board in one comma-separated CSV field.
            string result = MainboardNameShortener.Shorten("Some, Odd, Vendor Ltd.", "Model, X");

            Assert.IsFalse(result.Contains(","));
            Assert.AreEqual("Some Odd Vendor Model X", result);
        }

        [DataTestMethod]
        [DataRow("ASUSTeK COMPUTER INC.", "ASUS")]
        [DataRow("ASUS", "ASUS")]
        [DataRow("Micro-Star International Co., Ltd.", "MSI")]
        [DataRow("ASUSTEKX SOMETHING", "ASUSTEKX SOMETHING")]
        public void ToBrand_MatchesOnWordBoundariesOnly(string manufacturer, string expected)
        {
            Assert.AreEqual(expected, MainboardNameShortener.ToBrand(manufacturer));
        }
    }
}
