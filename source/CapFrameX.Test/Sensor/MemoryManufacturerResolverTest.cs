using CapFrameX.SystemInfo.NetStandard;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class MemoryManufacturerResolverTest
    {
        [TestMethod]
        [DataRow("G Skill Intl", "G.SKILL")]
        [DataRow("G.Skill", "G.SKILL")]
        [DataRow("GSkill", "G.SKILL")]
        [DataRow("04CD", "G.SKILL")]
        [DataRow(" 04cd ", "G.SKILL")]
        [DataRow("80CE", "Samsung")]
        [DataRow("ADATA", "ADATA")]
        public void Normalize_ManufacturerNamesAndJedecIds_ReturnsBrand(string raw, string expected)
        {
            Assert.AreEqual(expected, MemoryManufacturerResolver.Normalize(raw));
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
        public void Normalize_UnusableManufacturer_ReturnsEmpty(string raw)
        {
            Assert.AreEqual(string.Empty, MemoryManufacturerResolver.Normalize(raw));
        }

        [TestMethod]
        public void Resolve_NoSpdInventory_UsesWmiManufacturer()
        {
            Assert.AreEqual("Kingston", MemoryManufacturerResolver.Resolve(null, "Kingston", 2));
            Assert.AreEqual("Kingston", MemoryManufacturerResolver.Resolve(new string[0], "Kingston", 2));
        }

        [TestMethod]
        public void Resolve_UnusableSpdManufacturers_UsesWmiManufacturer()
        {
            var spdManufacturers = new[] { null, "Unknown", "1234", "Default string" };

            Assert.AreEqual("Kingston", MemoryManufacturerResolver.Resolve(spdManufacturers, "Kingston", 2));
        }

        [TestMethod]
        public void Resolve_CompleteSpdInventory_OverridesWmiChipManufacturer()
        {
            var spdManufacturers = new[] { "G Skill Intl", "04CD" };

            Assert.AreEqual("G.SKILL", MemoryManufacturerResolver.Resolve(spdManufacturers, "Samsung", 2));
        }

        [TestMethod]
        public void Resolve_CompleteMixedSpdInventory_KeepsDistinctModuleManufacturers()
        {
            var spdManufacturers = new[] { "G Skill Intl", "Kingston", "04CD", "kingston" };

            Assert.AreEqual("G.SKILL / Kingston",
                MemoryManufacturerResolver.Resolve(spdManufacturers, "Samsung", 4));
        }

        [TestMethod]
        public void Resolve_PartialSpdInventory_PreservesOtherWmiManufacturers()
        {
            var spdManufacturers = new[] { "G Skill Intl" };

            Assert.AreEqual("G.SKILL / Kingston",
                MemoryManufacturerResolver.Resolve(spdManufacturers, "G.SKILL / Kingston", 2));
        }

        [TestMethod]
        public void Resolve_PartialSpdInventory_MergesSpdFirstWithoutCaseSensitiveDuplicates()
        {
            var spdManufacturers = new[] { "G Skill Intl", "Corsair" };

            Assert.AreEqual("G.SKILL / Corsair / Kingston",
                MemoryManufacturerResolver.Resolve(spdManufacturers, "corsair / Kingston / G.SKILL", 4));
        }

        [TestMethod]
        public void Resolve_UnusableSpdModules_DoNotMakePartialInventoryComplete()
        {
            var spdManufacturers = new[] { "G Skill Intl", "Unknown" };

            Assert.AreEqual("G.SKILL / Kingston",
                MemoryManufacturerResolver.Resolve(spdManufacturers, "Kingston", 2));
        }

        [TestMethod]
        public void Resolve_LateSpdDetection_ReplacesInitialWmiFallback()
        {
            Assert.AreEqual("Samsung", MemoryManufacturerResolver.Resolve(new string[0], "Samsung", 2));
            Assert.AreEqual("G.SKILL",
                MemoryManufacturerResolver.Resolve(new[] { "G Skill Intl", "04CD" }, "Samsung", 2));
        }

        [TestMethod]
        public void Resolve_NoWmiModules_UsesSpdManufacturer()
        {
            Assert.AreEqual("G.SKILL",
                MemoryManufacturerResolver.Resolve(new[] { "04CD" }, string.Empty, 0));
        }

        [TestMethod]
        public void Resolve_NoUsableSource_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty,
                MemoryManufacturerResolver.Resolve(new[] { "Unknown" }, string.Empty, 2));
        }
    }
}
