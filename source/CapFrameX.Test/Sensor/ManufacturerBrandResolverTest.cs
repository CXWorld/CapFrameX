using CapFrameX.SystemInfo.NetStandard;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class ManufacturerBrandResolverTest
    {
        [TestMethod]
        [DataRow("ASUSTeK COMPUTER INC.", "ASUS")]
        [DataRow("ASUS", "ASUS")]
        [DataRow("Micro-Star International Co., Ltd.", "MSI")]
        [DataRow("ASUSTEKX SOMETHING", "ASUSTEKX SOMETHING")]
        public void ToBrand_MatchesOnWordBoundariesOnly(string manufacturer, string expected)
        {
            Assert.AreEqual(expected, ManufacturerBrandResolver.ToBrand(manufacturer));
        }

        [TestMethod]
        [DataRow("G Skill In", "G.SKILL")]
        [DataRow("G Skill Intl", "G.SKILL")]
        [DataRow("G.Skill", "G.SKILL")]
        [DataRow("G-Skill", "G.SKILL")]
        [DataRow("GSkill", "G.SKILL")]
        [DataRow("Team Group Inc", "Team Group")]
        [DataRow("Team Group Inc.", "Team Group")]
        [DataRow("A-DATA Technology", "ADATA")]
        [DataRow("ADATA Technology", "ADATA")]
        [DataRow("Micron Technology", "Micron")]
        [DataRow("Crucial Technology", "Crucial")]
        public void ToBrand_MapsMemoryVendorSpellings(string manufacturer, string expected)
        {
            Assert.AreEqual(expected, ManufacturerBrandResolver.ToBrand(manufacturer));
        }

        [TestMethod]
        [DataRow("G Skill Intl", "G.SKILL")]
        [DataRow("G.Skill", "G.SKILL")]
        [DataRow("GSkill", "G.SKILL")]
        [DataRow("04CD", "G.SKILL")]
        [DataRow(" 04cd ", "G.SKILL")]
        [DataRow("80CE", "Samsung")]
        [DataRow("ADATA", "ADATA")]
        public void ToMemoryBrand_ManufacturerNamesAndJedecIds_ReturnsBrand(string raw, string expected)
        {
            Assert.AreEqual(expected, ManufacturerBrandResolver.ToMemoryBrand(raw));
        }

        [TestMethod]
        // JEDEC ID next to the JEP106 name RAMSPDToolkit decodes from SPD for the same vendor.
        [DataRow("802C", "Micron Technology")]
        [DataRow("2C00", "Micron Technology")]
        [DataRow("80AD", "SK Hynix")]
        [DataRow("AD00", "SK Hynix")]
        [DataRow("80CE", "Samsung")]
        [DataRow("CE00", "Samsung")]
        [DataRow("859B", "Crucial Technology")]
        [DataRow("029E", "Corsair")]
        [DataRow("04CB", "A-DATA Technology")]
        [DataRow("04CD", "G Skill Intl")]
        [DataRow("04EF", "Team Group Inc")]
        [DataRow("0198", "Kingston")]
        [DataRow("7F98", "Kingston")]
        public void ToMemoryBrand_JedecIdAndSpdName_AgreeOnBrand(string jedecId, string spdName)
        {
            Assert.AreEqual(ManufacturerBrandResolver.ToMemoryBrand(jedecId),
                ManufacturerBrandResolver.ToMemoryBrand(spdName));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("Unknown")]
        [DataRow("To be filled by O.E.M.")]
        [DataRow("Default string")]
        [DataRow("System manufacturer")]
        [DataRow("1234")]
        public void ToMemoryBrand_UnusableManufacturer_ReturnsEmpty(string raw)
        {
            Assert.AreEqual(string.Empty, ManufacturerBrandResolver.ToMemoryBrand(raw));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_NoSpdInventory_UsesWmiManufacturer()
        {
            Assert.AreEqual("Kingston", ManufacturerBrandResolver.ResolveMemoryManufacturer(null, "Kingston", 2));
            Assert.AreEqual("Kingston", ManufacturerBrandResolver.ResolveMemoryManufacturer(new string[0], "Kingston", 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_UnusableSpdManufacturers_UsesWmiManufacturer()
        {
            var spdManufacturers = new[] { null, "Unknown", "1234", "Default string" };

            Assert.AreEqual("Kingston", ManufacturerBrandResolver.ResolveMemoryManufacturer(spdManufacturers, "Kingston", 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_CompleteSpdInventory_OverridesWmiChipManufacturer()
        {
            var spdManufacturers = new[] { "G Skill Intl", "04CD" };

            Assert.AreEqual("G.SKILL", ManufacturerBrandResolver.ResolveMemoryManufacturer(spdManufacturers, "Samsung", 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_CompleteMixedSpdInventory_KeepsDistinctModuleManufacturers()
        {
            var spdManufacturers = new[] { "G Skill Intl", "Kingston", "04CD", "kingston" };

            Assert.AreEqual("G.SKILL / Kingston",
                ManufacturerBrandResolver.ResolveMemoryManufacturer(spdManufacturers, "Samsung", 4));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_PartialSpdInventory_PreservesOtherWmiManufacturers()
        {
            var spdManufacturers = new[] { "G Skill Intl" };

            Assert.AreEqual("G.SKILL / Kingston",
                ManufacturerBrandResolver.ResolveMemoryManufacturer(spdManufacturers, "G.SKILL / Kingston", 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_PartialSpdInventory_MergesSpdFirstWithoutCaseSensitiveDuplicates()
        {
            var spdManufacturers = new[] { "G Skill Intl", "Corsair" };

            Assert.AreEqual("G.SKILL / Corsair / Kingston",
                ManufacturerBrandResolver.ResolveMemoryManufacturer(spdManufacturers, "corsair / Kingston / G.SKILL", 4));
        }

        [TestMethod]
        [DataRow("Team Group Inc", "04EF", "Team Group")]
        [DataRow("A-DATA Technology", "04CB", "ADATA")]
        public void ResolveMemoryManufacturer_SpdNameAndWmiJedecId_MergeIntoOneBrand(
            string spdManufacturer, string wmiManufacturer, string expected)
        {
            Assert.AreEqual(expected,
                ManufacturerBrandResolver.ResolveMemoryManufacturer(new[] { spdManufacturer }, wmiManufacturer, 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_UnusableSpdModules_DoNotMakePartialInventoryComplete()
        {
            var spdManufacturers = new[] { "G Skill Intl", "Unknown" };

            Assert.AreEqual("G.SKILL / Kingston",
                ManufacturerBrandResolver.ResolveMemoryManufacturer(spdManufacturers, "Kingston", 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_LateSpdDetection_ReplacesInitialWmiFallback()
        {
            Assert.AreEqual("Samsung", ManufacturerBrandResolver.ResolveMemoryManufacturer(new string[0], "Samsung", 2));
            Assert.AreEqual("G.SKILL",
                ManufacturerBrandResolver.ResolveMemoryManufacturer(new[] { "G Skill Intl", "04CD" }, "Samsung", 2));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_NoWmiModules_UsesSpdManufacturer()
        {
            Assert.AreEqual("G.SKILL",
                ManufacturerBrandResolver.ResolveMemoryManufacturer(new[] { "04CD" }, string.Empty, 0));
        }

        [TestMethod]
        public void ResolveMemoryManufacturer_NoUsableSource_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty,
                ManufacturerBrandResolver.ResolveMemoryManufacturer(new[] { "Unknown" }, string.Empty, 2));
        }
    }
}
