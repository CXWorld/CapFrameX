using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data.DTO;
using System;
using System.Linq;

namespace CapFrameX.Test.Webservice
{
    internal static class WebserviceTestData
    {
        public static readonly Guid CollectionId = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        public static readonly Guid UserId = Guid.Parse("1b4e28ba-2fa1-11d2-883f-0016d3cca427");
        public static readonly Guid FileId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        public static readonly Guid SecondFileId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

        // 15:09:26 at UTC+2 is 13:09:26 UTC
        public static readonly DateTimeOffset Created = new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.FromHours(2));
        public static readonly DateTime CreatedUtc = new DateTime(2026, 3, 14, 13, 9, 26, DateTimeKind.Utc);

        public static SqSessionCollection CreateCollection(Guid? sub, string description, params SqSessionData[] sessions)
        {
            var collection = new SqSessionCollection
            {
                Id = CollectionId,
                Created = Created
            };
            collection.Data.Sub = sub;
            collection.Data.Description = description;
            collection.Data.Sessions = sessions;
            return collection;
        }

        public static SqSessionData CreateSessionData(string hash, params string[] files)
        {
            return new SqSessionData
            {
                File = files,
                Hash = hash,
                AppVersion = "1.9.2.0",
                ProcessName = "game.exe",
                GameName = "Game " + hash,
                Comment = "Comment " + hash,
                CreationDate = new DateTime(2026, 2, 1, 20, 15, 0, DateTimeKind.Local),
                Cpu = "Cpu",
                Gpu = "Gpu",
                Mainboard = "Mainboard",
                Ram = "Ram",
                AverageFps = 144.5,
                P1Fps = 98.25
            };
        }

        public static SessionInfo CreateSessionInfo()
        {
            return new SessionInfo
            {
                AppVersion = new Version(1, 9, 2, 0),
                Comment = "Benchmark run",
                CreationDate = new DateTime(2026, 4, 5, 18, 30, 0, DateTimeKind.Local),
                GameName = "Cyberpunk 2077",
                ProcessName = "Cyberpunk2077.exe",
                Processor = "AMD Ryzen 7 9800X3D",
                GPU = "NVIDIA GeForce RTX 5090",
                Motherboard = "ASUS ROG Crosshair X870E Hero",
                SystemRam = "2x16GB DDR5-6000"
            };
        }

        /// <summary>
        /// One run of <paramref name="frameCount"/> frames, each <paramref name="frametime"/> ms long.
        /// </summary>
        public static Session CreateSession(string hash, int frameCount = 200, double frametime = 10)
        {
            var times = Enumerable.Range(0, frameCount).Select(i => i * frametime / 1000).ToArray();
            var frametimes = Enumerable.Repeat(frametime, frameCount).ToArray();
            var captureData = new SessionCaptureData(frameCount)
            {
                TimeInSeconds = times,
                MsBetweenPresents = frametimes,
                MsBetweenDisplayChange = frametimes.ToArray(),
                GpuActive = frametimes.ToArray(),
                CpuActive = frametimes.ToArray()
            };
            var session = new Session
            {
                Hash = hash,
                Info = CreateSessionInfo()
            };
            session.Runs.Add(new SessionRun { Hash = hash + "-run", CaptureData = captureData });
            return session;
        }
    }
}
