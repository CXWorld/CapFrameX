using System;
using System.IO;
using System.Linq;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookLearnedProfileStoreTest
    {
        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cfx-learned-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            try { Directory.Delete(_folder, true); } catch { }
        }

        [TestMethod]
        public void Upsert_RoundTripsThroughTheFile()
        {
            HookLearnedProfileStore store = HookLearnedProfileStore.Create(_folder);
            IHookLearnedProfileStore api = store;
            int changes = 0;
            using (store.Changes.Subscribe(_ => changes++))
            {
                api.Upsert("Resonance.exe", @"E:\Games\Resonance\Resonance.exe",
                    "dxgiproxy+sl", null, "abcd1234", e =>
                    {
                        e.SetStage(HookCompatibilityStage.Create(
                            HookCompatibilityStageId.GenericNoFfxLifecycle));
                        e.Verified = true;
                        e.LastVerdict = "Success";
                    });
            }

            Assert.AreEqual(1, changes);
            Assert.IsTrue(File.Exists(Path.Combine(_folder, HookLearnedProfileStore.FileName)));
            var reloaded = new HookLearnedProfileStore(store.StorePath);
            Assert.IsTrue(((IHookLearnedProfileStore)reloaded).TryGet("resonance", "dxgiproxy+sl",
                out HookLearnedProfileEntry entry));
            Assert.AreEqual(HookCompatibilityStageId.GenericNoFfxLifecycle, entry.StageId);
            Assert.IsTrue(entry.Verified);
            Assert.IsTrue(entry.MatchesHookBuild("ABCD1234"));
            Assert.AreEqual("generic D3D12 + no FidelityFX lifecycle hooks",
                reloaded.GetForProcess("Resonance").Single().StageName);
        }

        [TestMethod]
        public void Lookup_FallsBackToTheEarlySignatureAndKeepsSignaturesApart()
        {
            IHookLearnedProfileStore store = new HookLearnedProfileStore(null);
            store.Upsert("game", null, "sl+sldlssg", "none", "h", e => e.Verified = true);
            store.Upsert("game", null, "sl", null, "h", e => e.Verified = false);

            Assert.IsTrue(store.TryGet("game", "sl+sldlssg", out HookLearnedProfileEntry exact));
            Assert.IsTrue(exact.Verified);
            Assert.IsTrue(store.TryGet("game", "sl", out HookLearnedProfileEntry other));
            Assert.IsFalse(other.Verified);
            Assert.IsFalse(store.TryGet("game", "xefg", out _));
            Assert.IsTrue(store.TryGetByEarlySignature("game", "none", out HookLearnedProfileEntry early));
            Assert.AreEqual("sl+sldlssg", early.EvidenceSignature);
            Assert.AreEqual(2, store.GetForExecutable("GAME.EXE").Count);
        }

        [TestMethod]
        public void PendingStage_IsPersistedAndClearedByAVerifiedOutcome()
        {
            IHookLearnedProfileStore store = new HookLearnedProfileStore(null);
            HookCompatibilityStage next = HookCompatibilityStage.Create(
                HookCompatibilityStageId.Generic, "d3d12.dll");
            store.Upsert("game", null, "sl", null, "h", e => e.SetPending(next, "restart"));

            store.TryGet("game", "sl", out HookLearnedProfileEntry pending);
            Assert.AreEqual("Generic+early", pending.PendingStage().Key);
            Assert.AreEqual("restart", pending.PendingReason);

            store.Upsert("game", null, "sl", null, "h", e =>
            {
                e.SetStage(next);
                e.Verified = true;
                e.SetPending(null, null);
            });
            store.TryGet("game", "sl", out HookLearnedProfileEntry verified);
            Assert.IsNull(verified.PendingStage());
            Assert.AreEqual("Generic+early", verified.ToStage().Key);
        }

        [TestMethod]
        public void Reset_EmptiesTheStoreAndTheFile()
        {
            HookLearnedProfileStore store = HookLearnedProfileStore.Create(_folder);
            ((IHookLearnedProfileStore)store).Upsert("game", null, "sl", null, "h", e => e.Verified = true);

            store.Reset();

            Assert.AreEqual(0, store.GetAll().Count);
            Assert.AreEqual(0, new HookLearnedProfileStore(store.StorePath).GetAll().Count);
        }

        [TestMethod]
        public void Load_SetsADamagedFileAsideAndStartsEmpty()
        {
            string path = Path.Combine(_folder, HookLearnedProfileStore.FileName);
            File.WriteAllText(path, "{ this is not json");

            var store = new HookLearnedProfileStore(path);

            Assert.AreEqual(0, store.GetAll().Count);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(Directory.GetFiles(_folder, "*.corrupt-*").Length == 1);
        }
    }
}
