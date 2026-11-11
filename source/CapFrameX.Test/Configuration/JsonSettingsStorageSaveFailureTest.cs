using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Configuration
{
    /// <summary>
    /// A save that fails every attempt used to end the save subscription with an error that Rx
    /// threw on the thread pool, which terminated CapFrameX (crash reports of 2026-11-09/10: the
    /// portable version under Controlled folder access, and in a deleted WinRAR folder).
    /// </summary>
    [TestClass]
    public class JsonSettingsStorageSaveFailureTest
    {
        private string _folder;
        private string SettingsPath => Path.Combine(_folder, "AppSettings.json");

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cfx-settings-save-failure-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SettingsPath, "{}");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(SettingsPath))
                File.SetAttributes(SettingsPath, FileAttributes.Normal);
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task AFailedSaveIsReportedAndTheNextChangeIsSaved()
        {
            var storage = CreateStorage();
            await storage.Load();
            var failures = new List<Exception>();
            using var subscription = storage.SaveFailures.Subscribe(exception =>
            {
                lock (failures)
                    failures.Add(exception);
            });

            // Denied like a write under Controlled folder access.
            File.SetAttributes(SettingsPath, FileAttributes.ReadOnly);
            storage.SetValue("Count", 1);
            await WaitForSave(storage);

            lock (failures)
            {
                Assert.AreEqual(1, failures.Count, "Every attempt failed, so one failure is reported.");
                Assert.IsInstanceOfType(failures[0], typeof(UnauthorizedAccessException));
            }
            Assert.AreEqual("{}", File.ReadAllText(SettingsPath));
            Assert.AreEqual(1, storage.GetValue<int>("Count"), "The setting stays in memory.");

            File.SetAttributes(SettingsPath, FileAttributes.Normal);
            storage.SetValue("Count", 2);
            await WaitForSave(storage);

            Assert.AreEqual(2, JObject.Parse(File.ReadAllText(SettingsPath)).Value<int>("Count"),
                "The save pipeline survived the failure.");
            lock (failures)
                Assert.AreEqual(1, failures.Count);
        }

        [TestMethod]
        public async Task AFailureBeforeTheSubscriptionIsStillReported()
        {
            var storage = CreateStorage();
            await storage.Load();

            // Gone like the WinRAR folder 3 s after the start.
            Directory.Delete(_folder, true);
            storage.SetValue("Count", 1);
            await WaitForSave(storage);

            Exception reported = null;
            using (storage.SaveFailures.Subscribe(exception => reported = exception))
            {
            }
            Assert.IsInstanceOfType(reported, typeof(DirectoryNotFoundException));
        }

        private JsonSettingsStorage CreateStorage()
        {
            var paths = new Mock<IPathService>();
            paths.SetupGet(path => path.ConfigFolder).Returns(_folder);
            return new JsonSettingsStorage(NullLogger<JsonSettingsStorage>.Instance, paths.Object);
        }

        // Before the fix, a failed save never completed the pending save either.
        private static Task WaitForSave(JsonSettingsStorage storage)
            => storage.WaitForPendingSaveAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
}
