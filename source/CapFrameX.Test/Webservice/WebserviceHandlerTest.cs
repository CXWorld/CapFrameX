using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Webservice.Data.Commands;
using CapFrameX.Webservice.Data.DTO;
using CapFrameX.Webservice.Data.Exceptions;
using CapFrameX.Webservice.Data.Extensions;
using CapFrameX.Webservice.Data.Interfaces;
using CapFrameX.Webservice.Data.Queries;
using CapFrameX.Webservice.Implementation.Handlers;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CapFrameX.Test.Webservice.WebserviceTestData;

namespace CapFrameX.Test.Webservice
{
    [TestClass]
    public class WebserviceHandlerTest
    {
        [TestMethod]
        public async Task AddProcessHandler_StoresMappedProcess()
        {
            ProcessListData stored = null;
            var service = new Mock<IProcessListService>();
            service.Setup(s => s.AddProcess(It.IsAny<ProcessListData>()))
                .Callback<ProcessListData>(data => stored = data)
                .Returns(Task.CompletedTask);
            IRequestHandler<AddProcessCommand, Unit> handler = new AddProcessHandler(service.Object, NullLogger<AddProcessHandler>.Instance);

            await handler.Handle(new AddProcessCommand
            {
                Process = new ProcessListDataDTO { Name = "game.exe", DisplayName = "Game", IsBlacklisted = true }
            }, CancellationToken.None);

            Assert.IsNotNull(stored);
            Assert.AreEqual("game.exe", stored.Name);
            Assert.AreEqual("Game", stored.DisplayName);
            Assert.IsTrue(stored.IsBlacklisted);
        }

        [TestMethod]
        public async Task AddProcessHandler_ServiceFailure_IsLoggedInsteadOfThrown()
        {
            var failure = new InvalidOperationException("squidex down");
            var service = new Mock<IProcessListService>();
            service.Setup(s => s.AddProcess(It.IsAny<ProcessListData>())).ThrowsAsync(failure);
            var logger = new Mock<ILogger<AddProcessHandler>>();
            IRequestHandler<AddProcessCommand, Unit> handler = new AddProcessHandler(service.Object, logger.Object);

            await handler.Handle(new AddProcessCommand { Process = new ProcessListDataDTO { Name = "game.exe" } }, CancellationToken.None);

            logger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                failure,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [TestMethod]
        public async Task GetProcessListHandler_ReturnsMappedProcesses()
        {
            var service = new Mock<IProcessListService>();
            service.Setup(s => s.GetProcessList()).ReturnsAsync(new[]
            {
                new ProcessListData { Name = "a.exe", DisplayName = "A", IsBlacklisted = false },
                new ProcessListData { Name = "b.exe", DisplayName = "B", IsBlacklisted = true }
            });

            var result = await new GetProcessListHandler(service.Object).Handle(new GetProcessListQuery(), CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "a.exe", "b.exe" }, result.Select(p => p.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "A", "B" }, result.Select(p => p.DisplayName).ToArray());
            CollectionAssert.AreEqual(new[] { false, true }, result.Select(p => p.IsBlacklisted).ToArray());
        }

        [TestMethod]
        public async Task GetProcessListHandler_MissingList_ReturnsEmptyArray()
        {
            var service = new Mock<IProcessListService>();
            service.Setup(s => s.GetProcessList()).ReturnsAsync((IEnumerable<ProcessListData>)null);

            var result = await new GetProcessListHandler(service.Object).Handle(new GetProcessListQuery(), CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.IsEmpty(result);
        }

        [TestMethod]
        public async Task GetSessionCollectionReducedByIdHandler_ReturnsMappedCollection()
        {
            var service = new Mock<ISessionService>();
            service.Setup(s => s.GetSessionCollection(CollectionId))
                .ReturnsAsync(CreateCollection(UserId, "Launch", CreateSessionData("a", FileId.ToString())));

            var result = await new GetSessionCollectionReducedByIdHandler(service.Object)
                .Handle(new GetSessionCollectionReducedByIdQuery { Id = CollectionId }, CancellationToken.None);

            Assert.AreEqual(CollectionId, result.Id);
            Assert.AreEqual(UserId, result.UserId);
            Assert.AreEqual(CreatedUtc, result.Timestamp);
            Assert.AreEqual("Launch", result.Description);
            var session = result.Sessions.Single();
            Assert.AreEqual("a", session.SessionHash);
            Assert.AreEqual(FileId, session.FileId);
        }

        [TestMethod]
        public async Task GetSessionCollectionReducedByIdHandler_UnknownId_Throws()
        {
            var service = new Mock<ISessionService>();
            service.Setup(s => s.GetSessionCollection(It.IsAny<Guid>())).ReturnsAsync((SqSessionCollection)null);
            var handler = new GetSessionCollectionReducedByIdHandler(service.Object);

            await Assert.ThrowsExactlyAsync<SessionCollectionNotFoundException>(() =>
                handler.Handle(new GetSessionCollectionReducedByIdQuery { Id = CollectionId }, CancellationToken.None));
        }

        [TestMethod]
        public async Task GetSessionCollectionsReducedByUserIdHandler_MapsEveryCollection()
        {
            var first = CreateCollection(UserId, "first", CreateSessionData("a", FileId.ToString()));
            var second = CreateCollection(UserId, "second");
            second.Id = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
            var service = new Mock<ISessionService>();
            service.Setup(s => s.GetSessionCollectionsForUser(UserId)).ReturnsAsync(new[] { first, second });
            var handler = new GetSessionCollectionsReducedByUserIdHandler(service.Object, new GetSessionCollectionsReducedForUserByIdQueryValidator());

            var result = (await handler.Handle(new GetSessionCollectionsReducedForUserByIdQuery { UserId = UserId }, CancellationToken.None)).ToArray();

            CollectionAssert.AreEqual(new[] { CollectionId, second.Id }, result.Select(c => c.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "first", "second" }, result.Select(c => c.Description).ToArray());
            Assert.AreEqual(FileId, result[0].Sessions.Single().FileId);
            Assert.IsEmpty(result[1].Sessions);
        }

        [TestMethod]
        public async Task GetSessionCollectionsReducedByUserIdHandler_EmptyUserId_IsRejected()
        {
            var service = new Mock<ISessionService>(MockBehavior.Strict);
            var handler = new GetSessionCollectionsReducedByUserIdHandler(service.Object, new GetSessionCollectionsReducedForUserByIdQueryValidator());

            await Assert.ThrowsExactlyAsync<ValidationException>(() =>
                handler.Handle(new GetSessionCollectionsReducedForUserByIdQuery { UserId = Guid.Empty }, CancellationToken.None));
        }

        [TestMethod]
        public async Task GetSessionCollectionByIdHandler_MapsHeaderAndLoadsSessions()
        {
            var compressed = CreateSession("compressed");
            var plain = CreateSession("plain");
            var service = new Mock<ISessionService>();
            service.Setup(s => s.GetSessionCollection(CollectionId)).ReturnsAsync(CreateCollection(UserId, "Launch",
                CreateSessionData("compressed", FileId.ToString()),
                CreateSessionData("plain", SecondFileId.ToString())));
            service.Setup(s => s.DownloadAsset(FileId))
                .ReturnsAsync(("compressed.json.gz", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(compressed)).Compress()));
            service.Setup(s => s.DownloadAsset(SecondFileId))
                .ReturnsAsync(("plain.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(plain))));
            var handler = new GetSessionCollectionByIdHandler(new GetCaptureByIdQueryValidator(), service.Object);

            var result = await handler.Handle(new GetSessionCollectionByIdQuery { Id = CollectionId }, CancellationToken.None);

            Assert.AreEqual(CollectionId, result.Id);
            Assert.AreEqual(UserId, result.UserId);
            Assert.AreEqual(CreatedUtc, result.Timestamp);
            Assert.AreEqual("Launch", result.Description);
            var sessions = result.Sessions.ToArray();
            CollectionAssert.AreEqual(new[] { "compressed", "plain" }, sessions.Select(s => s.Hash).ToArray());
            Assert.AreEqual(compressed.Info.GameName, sessions[0].Info.GameName);
            Assert.AreEqual(200, sessions[1].Runs.Single().CaptureData.TimeInSeconds.Length);
        }

        [TestMethod]
        public async Task GetSessionCollectionByIdHandler_UnknownId_Throws()
        {
            var service = new Mock<ISessionService>();
            service.Setup(s => s.GetSessionCollection(It.IsAny<Guid>())).ReturnsAsync((SqSessionCollection)null);
            var handler = new GetSessionCollectionByIdHandler(new GetCaptureByIdQueryValidator(), service.Object);

            await Assert.ThrowsExactlyAsync<SessionCollectionNotFoundException>(() =>
                handler.Handle(new GetSessionCollectionByIdQuery { Id = CollectionId }, CancellationToken.None));
        }

        [TestMethod]
        public async Task GetSessionCollectionByIdHandler_EmptyId_IsRejected()
        {
            var service = new Mock<ISessionService>(MockBehavior.Strict);
            var handler = new GetSessionCollectionByIdHandler(new GetCaptureByIdQueryValidator(), service.Object);

            await Assert.ThrowsExactlyAsync<ValidationException>(() =>
                handler.Handle(new GetSessionCollectionByIdQuery { Id = Guid.Empty }, CancellationToken.None));
        }

        [TestMethod]
        public async Task UploadCapturesHandler_StoresMappedSessionsWithAssetAndMetrics()
        {
            var session = CreateSession("upload-hash", frameCount: 200, frametime: 10);
            var uploads = new List<(byte[] Data, string FileName)>();
            SqSessionCollectionData saved = null;
            var savedId = Guid.Parse("c56a4180-65aa-42ec-a945-5fd21dec0538");
            var service = new Mock<ISessionService>();
            service.Setup(s => s.UploadAsset(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Callback<byte[], string>((data, fileName) => uploads.Add((data, fileName)))
                .ReturnsAsync(FileId);
            service.Setup(s => s.SaveSessionCollection(It.IsAny<SqSessionCollectionData>()))
                .Callback<SqSessionCollectionData>(data => saved = data)
                .ReturnsAsync(savedId);
            var handler = new UploadCapturesHandler(new UploadSessionsCommandValidator(), service.Object);

            var result = await handler.Handle(new UploadSessionsCommand
            {
                UserId = UserId,
                Description = "Upload",
                Sessions = new ISession[] { session }
            }, CancellationToken.None);

            Assert.AreEqual(savedId, result);
            Assert.AreEqual(UserId, saved.Sub);
            Assert.AreEqual("Upload", saved.Description);
            var data = saved.Sessions.Single();
            Assert.AreEqual("upload-hash", data.Hash);
            Assert.AreEqual("1.9.2.0", data.AppVersion);
            Assert.AreEqual(session.Info.Comment, data.Comment);
            Assert.AreEqual(session.Info.CreationDate, data.CreationDate);
            Assert.AreEqual(session.Info.GameName, data.GameName);
            Assert.AreEqual(session.Info.ProcessName, data.ProcessName);
            Assert.AreEqual(session.Info.Processor, data.Cpu);
            Assert.AreEqual(session.Info.GPU, data.Gpu);
            Assert.AreEqual(session.Info.Motherboard, data.Mainboard);
            Assert.AreEqual(session.Info.SystemRam, data.Ram);
            CollectionAssert.AreEqual(new[] { FileId.ToString() }, data.File);
            Assert.AreEqual(100d, data.AverageFps, 0.01);
            Assert.AreEqual(100d, data.P1Fps, 0.01);

            var upload = uploads.Single();
            Assert.AreEqual("upload-hash.json.gz", upload.FileName);
            var uploaded = JsonConvert.DeserializeObject<Session>(Encoding.UTF8.GetString(upload.Data.Decompress()));
            Assert.AreEqual("upload-hash", uploaded.Hash);
            Assert.AreEqual(session.Info.GameName, uploaded.Info.GameName);
        }

        [TestMethod]
        public async Task UploadCapturesHandler_UploadsEverySessionSeparately()
        {
            var uploadedFiles = new List<string>();
            var assetIds = new Queue<Guid>(new[] { FileId, SecondFileId });
            SqSessionCollectionData saved = null;
            var service = new Mock<ISessionService>();
            service.Setup(s => s.UploadAsset(It.IsAny<byte[]>(), It.IsAny<string>()))
                .Callback<byte[], string>((_, fileName) => uploadedFiles.Add(fileName))
                .ReturnsAsync(() => assetIds.Dequeue());
            service.Setup(s => s.SaveSessionCollection(It.IsAny<SqSessionCollectionData>()))
                .Callback<SqSessionCollectionData>(data => saved = data)
                .ReturnsAsync(CollectionId);
            var handler = new UploadCapturesHandler(new UploadSessionsCommandValidator(), service.Object);

            await handler.Handle(new UploadSessionsCommand
            {
                Sessions = new ISession[] { CreateSession("first", frametime: 10), CreateSession("second", frametime: 20) }
            }, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "first.json.gz", "second.json.gz" }, uploadedFiles);
            Assert.IsNull(saved.Sub);
            CollectionAssert.AreEqual(new[] { "first", "second" }, saved.Sessions.Select(s => s.Hash).ToArray());
            CollectionAssert.AreEqual(new[] { FileId.ToString(), SecondFileId.ToString() }, saved.Sessions.Select(s => s.File.Single()).ToArray());
            Assert.AreEqual(100d, saved.Sessions[0].AverageFps, 0.01);
            Assert.AreEqual(50d, saved.Sessions[1].AverageFps, 0.01);
        }

        [TestMethod]
        public async Task UploadCapturesHandler_SessionWithoutRuns_IsRejected()
        {
            var service = new Mock<ISessionService>(MockBehavior.Strict);
            var session = CreateSession("no-runs");
            session.Runs.Clear();
            var handler = new UploadCapturesHandler(new UploadSessionsCommandValidator(), service.Object);

            await Assert.ThrowsExactlyAsync<ValidationException>(() =>
                handler.Handle(new UploadSessionsCommand { Sessions = new ISession[] { session } }, CancellationToken.None));
        }

        [TestMethod]
        public async Task SearchSessionsHandler_ForwardsEveryFilter()
        {
            var match = CreateSessionData("match", FileId.ToString());
            var service = new Mock<ISessionService>();
            service.Setup(s => s.SearchSessions("cpu", "gpu", "board", "ram", "game", "comment"))
                .ReturnsAsync(new List<SqSessionData> { match });

            var result = await new SearchSessionsHandler(service.Object).Handle(new SearchSessionsQuery
            {
                Cpu = "cpu",
                Gpu = "gpu",
                Mainbaord = "board",
                Ram = "ram",
                GameName = "game",
                Comment = "comment"
            }, CancellationToken.None);

            Assert.AreSame(match, result.Single());
        }
    }
}
