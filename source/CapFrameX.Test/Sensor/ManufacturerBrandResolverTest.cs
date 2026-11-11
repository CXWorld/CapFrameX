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
        // JEP106 names whose company name differs from the brand on the box.
        [DataRow("Patriot Memory (PDP Systems)", "Patriot")]
        [DataRow("Silicon Power Computer & Communications", "Silicon Power")]
        [DataRow("Mushkin Enhanced Memory", "Mushkin")]
        [DataRow("Golden Empire", "GeIL")]
        [DataRow("Essencore Limited", "KLEVV")]
        [DataRow("Transcend Information", "Transcend")]
        [DataRow("Kingmax Semiconductor", "Kingmax")]
        [DataRow("Lexar Media", "Lexar")]
        [DataRow("Gloway International (HK)", "Gloway")]
        [DataRow("Biwin Semiconductor (HK) Co Ltd", "BIWIN")]
        [DataRow("Biwin Storage Technology Co Ltd", "BIWIN")]
        [DataRow("Galaxy Microsystems Ltd", "GALAX")]
        [DataRow("Shenzhen Fanxiang Information Technology", "Fanxiang")]
        [DataRow("Shenzhen Longsys Electronics Co Ltd", "Longsys")]
        [DataRow("ShenZhen Juhor Precision Tech Co Ltd", "JUHOR")]
        // Unrelated companies sharing a prefix keep their own name.
        [DataRow("Patriot Scientific", "Patriot Scientific")]
        [DataRow("Goldenram", "Goldenram")]
        [DataRow("Galaxy Power", "Galaxy Power")]
        public void ToBrand_MapsJep106CompanyNamesToBrands(string manufacturer, string expected)
        {
            Assert.AreEqual(expected, ManufacturerBrandResolver.ToBrand(manufacturer));
        }

        [TestMethod]
        // Number of continuation codes in the low byte, manufacturer code in the high byte, both
        // with their odd parity bit, as SPD and SMBIOS store them.
        [DataRow((ushort)0xCD04, "G.SKILL")]
        [DataRow((ushort)0xEF04, "Team Group")]
        [DataRow((ushort)0xCB04, "ADATA")]
        [DataRow((ushort)0x9801, "Kingston")]
        [DataRow((ushort)0x9E02, "Corsair")]
        [DataRow((ushort)0xCE80, "Samsung")]
        [DataRow((ushort)0x2C80, "Micron")]
        [DataRow((ushort)0xAD80, "SK Hynix")]
        [DataRow((ushort)0x9B85, "Crucial")]
        [DataRow((ushort)0x0285, "Patriot")]
        [DataRow((ushort)0xD386, "Silicon Power")]
        [DataRow((ushort)0x3201, "Mushkin")]
        [DataRow((ushort)0x1383, "GeIL")]
        [DataRow((ushort)0x9808, "KLEVV")]
        [DataRow((ushort)0x768A, "Lexar")]
        [DataRow((ushort)0x9208, "GALAX")]
        public void FromJedecId_KnownModuleManufacturer_ReturnsBrand(ushort moduleManufacturerId, string expected)
        {
            Assert.AreEqual(expected, ManufacturerBrandResolver.FromJedecId(moduleManufacturerId));
        }

        [TestMethod]
        [DataRow((ushort)0x0000)]
        [DataRow((ushort)0xFF04)]
        [DataRow((ushort)0x7F7F)]
        public void FromJedecId_UnknownModuleManufacturer_ReturnsEmpty(ushort moduleManufacturerId)
        {
            Assert.AreEqual(string.Empty, ManufacturerBrandResolver.FromJedecId(moduleManufacturerId));
        }

        [TestMethod]
        // WMI spells the same two bytes as hex text, continuation codes first.
        [DataRow("802C", (ushort)0x2C80)]
        [DataRow("80AD", (ushort)0xAD80)]
        [DataRow("80CE", (ushort)0xCE80)]
        [DataRow("859B", (ushort)0x9B85)]
        [DataRow("029E", (ushort)0x9E02)]
        [DataRow("04CB", (ushort)0xCB04)]
        [DataRow("04CD", (ushort)0xCD04)]
        [DataRow("04EF", (ushort)0xEF04)]
        [DataRow("0198", (ushort)0x9801)]
        public void FromJedecId_AgreesWithWmiJedecIdTable(string wmiId, ushort moduleManufacturerId)
        {
            Assert.AreEqual(ManufacturerBrandResolver.ToMemoryBrand(wmiId),
                ManufacturerBrandResolver.FromJedecId(moduleManufacturerId));
        }

        [TestMethod]
        // The ID is the raw SPD value; the string is the firmware's reading of it, or of the chips.
        [DataRow((ushort)0xCD04, "Samsung", "F4-3000C15-16GVR", "G.SKILL")]
        [DataRow((ushort)0xCD04, "Unknown", "F4-3000C15-16GVR", "G.SKILL")]
        [DataRow((ushort)0x0285, "Unknown", "PVS416G320C6K", "Patriot")]
        [DataRow((ushort)0x0000, "Kingston", "KF432C16BB/8", "Kingston")]
        [DataRow((ushort)0xFF04, "Corsair", "CMK16GX4M2B3200C16", "Corsair")]
        [DataRow((ushort)0x0000, "Unknown", "F4-3000C15-16GVR", "G.SKILL")]
        [DataRow((ushort)0x0000, "Unknown", "CMK16GX4M2B3200C16", "")]
        public void ToMemoryBrand_SmbiosRecord_PrefersModuleManufacturerId(
            ushort moduleManufacturerId, string raw, string partNumber, string expected)
        {
            Assert.AreEqual(expected, ManufacturerBrandResolver.ToMemoryBrand(moduleManufacturerId, raw, partNumber));
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
        // The first row is what a user's BIOS reported for a G.Skill Ripjaws V kit.
        [DataRow("Unknown", "F4-3000C15-16GVR")]
        [DataRow(null, "F5-7800J3646H16G")]
        [DataRow("", "F3-12800CL9-8GBXL")]
        [DataRow("To be filled by O.E.M.", "F2-6400CL5-2GBNQ")]
        [DataRow("04FF", "F4-3200C16-8GVKB")]
        [DataRow("Unknown", "  F4-3000C15-16GVR  ")]
        public void ToMemoryBrand_UnusableManufacturerWithGSkillPartNumber_ReturnsGSkill(string raw, string partNumber)
        {
            Assert.AreEqual("G.SKILL", ManufacturerBrandResolver.ToMemoryBrand(raw, partNumber));
        }

        [TestMethod]
        public void ToMemoryBrand_UsableManufacturer_WinsOverPartNumber()
        {
            Assert.AreEqual("Kingston", ManufacturerBrandResolver.ToMemoryBrand("Kingston", "F4-3000C15-16GVR"));
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("CMK16GX4M2B3200C16")]
        [DataRow("KF432C16BB/8")]
        [DataRow("F4")]
        [DataRow("F4-")]
        [DataRow("F4-ABC")]
        [DataRow("XF4-3000C15-16GVR")]
        public void ToMemoryBrand_UnusableManufacturerWithOtherPartNumber_ReturnsEmpty(string partNumber)
        {
            Assert.AreEqual(string.Empty, ManufacturerBrandResolver.ToMemoryBrand("Unknown", partNumber));
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
