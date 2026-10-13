using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Mappings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Linq;
using static CapFrameX.Test.Webservice.WebserviceTestData;

namespace CapFrameX.Test.Webservice
{
    [TestClass]
    public class SessionCollectionMappingsTest
    {
        [TestMethod]
        public void ToSessionCollectionDto_MapsHeader()
        {
            var collection = CreateCollection(UserId, "Launch benchmarks", CreateSessionData("a", FileId.ToString()));

            var dto = collection.ToSessionCollectionDto();

            Assert.AreEqual(CollectionId, dto.Id);
            Assert.AreEqual(UserId, dto.UserId);
            Assert.AreEqual("Launch benchmarks", dto.Description);
        }

        [TestMethod]
        public void ToSessionCollectionDto_ConvertsCreatedToUtc()
        {
            var dto = CreateCollection(UserId, null).ToSessionCollectionDto();

            Assert.AreEqual(CreatedUtc, dto.Timestamp);
            Assert.AreEqual(DateTimeKind.Utc, dto.Timestamp.Kind);
        }

        [TestMethod]
        public void ToSessionCollectionDto_LeavesSessionsToTheCaller()
        {
            var collection = CreateCollection(UserId, null, CreateSessionData("a", FileId.ToString()));

            Assert.IsNull(collection.ToSessionCollectionDto().Sessions);
        }

        [TestMethod]
        public void ToSessionCollectionDto_AnonymousCollectionHasNoUser()
        {
            Assert.IsNull(CreateCollection(null, null).ToSessionCollectionDto().UserId);
        }

        [TestMethod]
        public void ToSessionCollectionDto_NullSource_ReturnsNull()
        {
            Assert.IsNull(((SqSessionCollection)null).ToSessionCollectionDto());
        }

        [TestMethod]
        public void ToSessionCollectionReducedDto_MapsHeader()
        {
            var dto = CreateCollection(UserId, "Launch benchmarks").ToSessionCollectionReducedDto();

            Assert.AreEqual(CollectionId, dto.Id);
            Assert.AreEqual(UserId, dto.UserId);
            Assert.AreEqual(CreatedUtc, dto.Timestamp);
            Assert.AreEqual(DateTimeKind.Utc, dto.Timestamp.Kind);
            Assert.AreEqual("Launch benchmarks", dto.Description);
        }

        [TestMethod]
        public void ToSessionCollectionReducedDto_MapsSessionsInOrder()
        {
            var collection = CreateCollection(UserId, null,
                CreateSessionData("a", FileId.ToString()),
                CreateSessionData("b", SecondFileId.ToString()));

            var sessions = collection.ToSessionCollectionReducedDto().Sessions.ToArray();

            CollectionAssert.AreEqual(new[] { "a", "b" }, sessions.Select(s => s.SessionHash).ToArray());
            CollectionAssert.AreEqual(new[] { FileId, SecondFileId }, sessions.Select(s => s.FileId).ToArray());
        }

        [TestMethod]
        public void ToSessionCollectionReducedDto_SessionsAreMaterialized()
        {
            var dto = CreateCollection(UserId, null, CreateSessionData("a", FileId.ToString())).ToSessionCollectionReducedDto();

            Assert.AreSame(dto.Sessions.First(), dto.Sessions.First());
        }

        [TestMethod]
        public void ToSessionCollectionReducedDto_MissingSessions_MapToEmptyList()
        {
            var collection = CreateCollection(UserId, null);
            collection.Data.Sessions = null;

            var dto = collection.ToSessionCollectionReducedDto();

            Assert.IsNotNull(dto.Sessions);
            Assert.IsEmpty(dto.Sessions);
        }

        [TestMethod]
        public void ToSessionCollectionReducedDto_KeepsNullSessionEntries()
        {
            var collection = CreateCollection(UserId, null, null, CreateSessionData("b", FileId.ToString()));

            var sessions = collection.ToSessionCollectionReducedDto().Sessions.ToArray();

            Assert.HasCount(2, sessions);
            Assert.IsNull(sessions[0]);
            Assert.AreEqual("b", sessions[1].SessionHash);
        }

        [TestMethod]
        public void ToSessionCollectionReducedDto_NullSource_ReturnsNull()
        {
            Assert.IsNull(((SqSessionCollection)null).ToSessionCollectionReducedDto());
        }

        [TestMethod]
        public void ToSessionReducedDto_MapsAllFields()
        {
            var source = CreateSessionData("hash-1", FileId.ToString());

            var dto = source.ToSessionReducedDto();

            Assert.AreEqual("hash-1", dto.SessionHash);
            Assert.AreEqual("1.9.2.0", dto.AppVersion);
            Assert.AreEqual("Comment hash-1", dto.Comment);
            Assert.AreEqual("game.exe", dto.ProcessName);
            Assert.AreEqual("Game hash-1", dto.GameName);
            Assert.AreEqual(source.CreationDate, dto.CreationDate);
            Assert.AreEqual(source.CreationDate.Kind, dto.CreationDate.Kind);
            Assert.AreEqual(FileId, dto.FileId);
        }

        [TestMethod]
        public void ToSessionReducedDto_UsesFirstFileAsFileId()
        {
            var dto = CreateSessionData("a", SecondFileId.ToString(), FileId.ToString()).ToSessionReducedDto();

            Assert.AreEqual(SecondFileId, dto.FileId);
        }

        [TestMethod]
        [DataRow("0F8FAD5B-D9CB-469F-A165-70867728950E")]
        [DataRow("{0f8fad5b-d9cb-469f-a165-70867728950e}")]
        [DataRow("0f8fad5bd9cb469fa16570867728950e")]
        public void ToSessionReducedDto_AcceptsEveryGuidFormat(string file)
        {
            Assert.AreEqual(FileId, CreateSessionData("a", file).ToSessionReducedDto().FileId);
        }

        [TestMethod]
        public void ToSessionReducedDto_WithoutFiles_HasEmptyFileId()
        {
            var noArray = CreateSessionData("a");
            noArray.File = null;

            Assert.AreEqual(Guid.Empty, noArray.ToSessionReducedDto().FileId);
            Assert.AreEqual(Guid.Empty, CreateSessionData("b").ToSessionReducedDto().FileId);
            Assert.AreEqual(Guid.Empty, CreateSessionData("c", new string[] { null }).ToSessionReducedDto().FileId);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("not-a-guid")]
        public void ToSessionReducedDto_InvalidFileId_Throws(string file)
        {
            var source = CreateSessionData("a", file);

            Assert.ThrowsExactly<FormatException>(() => source.ToSessionReducedDto());
        }

        [TestMethod]
        public void ToSessionReducedDto_NullSource_ReturnsNull()
        {
            Assert.IsNull(((SqSessionData)null).ToSessionReducedDto());
        }

        [TestMethod]
        public void ToSqSessionData_MapsSessionInfo()
        {
            var session = CreateSession("session-hash");

            var data = session.ToSqSessionData();

            Assert.AreEqual("session-hash", data.Hash);
            Assert.AreEqual("1.9.2.0", data.AppVersion);
            Assert.AreEqual("Benchmark run", data.Comment);
            Assert.AreEqual(session.Info.CreationDate, data.CreationDate);
            Assert.AreEqual("Cyberpunk 2077", data.GameName);
            Assert.AreEqual("Cyberpunk2077.exe", data.ProcessName);
            Assert.AreEqual("AMD Ryzen 7 9800X3D", data.Cpu);
            Assert.AreEqual("NVIDIA GeForce RTX 5090", data.Gpu);
            Assert.AreEqual("ASUS ROG Crosshair X870E Hero", data.Mainboard);
            Assert.AreEqual("2x16GB DDR5-6000", data.Ram);
        }

        [TestMethod]
        public void ToSqSessionData_LeavesAssetAndMetricsToTheCaller()
        {
            var data = CreateSession("session-hash").ToSqSessionData();

            Assert.IsNull(data.File);
            Assert.AreEqual(0d, data.AverageFps);
            Assert.AreEqual(0d, data.P1Fps);
        }

        [TestMethod]
        [DataRow("1.9.2.0")]
        [DataRow("1.8.0")]
        public void ToSqSessionData_FormatsAppVersion(string version)
        {
            var session = CreateSession("a");
            session.Info.AppVersion = Version.Parse(version);

            Assert.AreEqual(version, session.ToSqSessionData().AppVersion);
        }

        [TestMethod]
        public void ToSqSessionData_WithoutAppVersion_HasNoAppVersion()
        {
            var session = CreateSession("a");
            session.Info.AppVersion = null;

            Assert.IsNull(session.ToSqSessionData().AppVersion);
        }

        [TestMethod]
        public void ToSqSessionData_WithoutInfo_MapsHashOnly()
        {
            var data = new Session { Hash = "only-hash", Info = null }.ToSqSessionData();

            Assert.AreEqual("only-hash", data.Hash);
            Assert.IsNull(data.AppVersion);
            Assert.IsNull(data.Comment);
            Assert.AreEqual(default, data.CreationDate);
            Assert.IsNull(data.GameName);
            Assert.IsNull(data.ProcessName);
            Assert.IsNull(data.Cpu);
            Assert.IsNull(data.Gpu);
            Assert.IsNull(data.Mainboard);
            Assert.IsNull(data.Ram);
        }

        [TestMethod]
        public void ToSqSessionData_AcceptsAnySessionImplementation()
        {
            var info = CreateSessionInfo();
            var session = new Mock<ISession>();
            session.SetupGet(s => s.Hash).Returns("mocked");
            session.SetupGet(s => s.Info).Returns(info);

            var data = session.Object.ToSqSessionData();

            Assert.AreEqual("mocked", data.Hash);
            Assert.AreEqual(info.Processor, data.Cpu);
        }

        [TestMethod]
        public void ToSqSessionData_NullSource_ReturnsNull()
        {
            Assert.IsNull(((ISession)null).ToSqSessionData());
        }
    }
}
