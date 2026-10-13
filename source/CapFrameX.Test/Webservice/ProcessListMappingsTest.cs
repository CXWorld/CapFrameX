using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Webservice
{
    [TestClass]
    public class ProcessListMappingsTest
    {
        [TestMethod]
        public void ToProcessListDataDto_CopiesAllFields()
        {
            var source = new ProcessListData { Name = "dwm.exe", DisplayName = "Desktop Window Manager", IsBlacklisted = true };

            var dto = source.ToProcessListDataDto();

            Assert.AreEqual("dwm.exe", dto.Name);
            Assert.AreEqual("Desktop Window Manager", dto.DisplayName);
            Assert.IsTrue(dto.IsBlacklisted);
        }

        [TestMethod]
        public void ToProcessListData_CopiesAllFields()
        {
            var source = new ProcessListDataDTO { Name = "game.exe", DisplayName = "Game", IsBlacklisted = false };

            var data = source.ToProcessListData();

            Assert.AreEqual("game.exe", data.Name);
            Assert.AreEqual("Game", data.DisplayName);
            Assert.IsFalse(data.IsBlacklisted);
        }

        [TestMethod]
        public void ProcessListMapping_RoundTripKeepsValues()
        {
            var source = new ProcessListDataDTO { Name = "launcher.exe", DisplayName = null, IsBlacklisted = true };

            var roundTrip = source.ToProcessListData().ToProcessListDataDto();

            Assert.AreNotSame(source, roundTrip);
            Assert.AreEqual(source.Name, roundTrip.Name);
            Assert.IsNull(roundTrip.DisplayName);
            Assert.AreEqual(source.IsBlacklisted, roundTrip.IsBlacklisted);
        }

        [TestMethod]
        public void ProcessListMapping_NullSource_ReturnsNull()
        {
            Assert.IsNull(((ProcessListData)null).ToProcessListDataDto());
            Assert.IsNull(((ProcessListDataDTO)null).ToProcessListData());
        }

        [TestMethod]
        public void ToProcessListDataDtos_MapsEveryEntryInOrder()
        {
            IEnumerable<ProcessListData> source = new[]
            {
                new ProcessListData { Name = "a.exe", DisplayName = "A", IsBlacklisted = true },
                new ProcessListData { Name = "b.exe", DisplayName = "B", IsBlacklisted = false },
                new ProcessListData { Name = "c.exe", DisplayName = "C", IsBlacklisted = true }
            }.Where(_ => true);

            var dtos = source.ToProcessListDataDtos();

            CollectionAssert.AreEqual(new[] { "a.exe", "b.exe", "c.exe" }, dtos.Select(dto => dto.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, dtos.Select(dto => dto.DisplayName).ToArray());
            CollectionAssert.AreEqual(new[] { true, false, true }, dtos.Select(dto => dto.IsBlacklisted).ToArray());
        }

        [TestMethod]
        public void ToProcessListDataDtos_NullList_ReturnsEmptyArray()
        {
            var dtos = ((IEnumerable<ProcessListData>)null).ToProcessListDataDtos();

            Assert.IsNotNull(dtos);
            Assert.IsEmpty(dtos);
        }

        [TestMethod]
        public void ToProcessListDataDtos_KeepsNullEntries()
        {
            var dtos = new[] { null, new ProcessListData { Name = "a.exe" } }.ToProcessListDataDtos();

            Assert.HasCount(2, dtos);
            Assert.IsNull(dtos[0]);
            Assert.AreEqual("a.exe", dtos[1].Name);
        }
    }
}
