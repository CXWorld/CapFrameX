using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CapFrameX.Test.Webservice
{
    [TestClass]
    public class AppNotificationMappingsTest
    {
        [TestMethod]
        public void ToAppNotificationDto_CopiesAllFields()
        {
            var timestamp = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Local);
            var source = new SqAppNotificationData
            {
                IsActive = true,
                Message = "Maintenance tonight",
                Timestamp = timestamp
            };

            var dto = source.ToAppNotificationDto();

            Assert.IsTrue(dto.IsActive);
            Assert.AreEqual("Maintenance tonight", dto.Message);
            Assert.AreEqual(timestamp, dto.Timestamp);
            Assert.AreEqual(DateTimeKind.Local, dto.Timestamp.Kind);
        }

        [TestMethod]
        public void ToAppNotificationDto_InactiveNotificationWithoutMessage()
        {
            var dto = new SqAppNotificationData().ToAppNotificationDto();

            Assert.IsFalse(dto.IsActive);
            Assert.IsNull(dto.Message);
            Assert.AreEqual(default, dto.Timestamp);
        }

        [TestMethod]
        public void ToAppNotificationDto_NullSource_ReturnsNull()
        {
            Assert.IsNull(((SqAppNotificationData)null).ToAppNotificationDto());
        }
    }
}
