using System;
using System.Collections.Generic;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The versions of the DLSS and XeSS frame generation libraries a game has loaded.
    /// </summary>
    [TestClass]
    public class FrameGenerationRuntimeModulesTest
    {
        private const string Game = @"E:\Games\Game\";
        private const string NgxCopy =
            @"C:\ProgramData\NVIDIA\NGX\models\dlssg\versions\20318480\files\160_E658700.bin";
        // Intel driver 32.0.101.9175 on an Arc B390 (2026-11-22).
        private const string IntelDriverRuntime =
            @"C:\Windows\System32\DriverStore\FileRepository\iigd_dch.inf_amd64_413e993a06fd9302\igxess_fg.dll";

        private static readonly Dictionary<string, RuntimeFileVersion> Files =
            new Dictionary<string, RuntimeFileVersion>(StringComparer.OrdinalIgnoreCase)
            {
                [Game + "nvngx_dlssg.dll"] = new RuntimeFileVersion(310, 2, 1, 0,
                    "NVIDIA DLSS-G MFGLW - DVS PRODUCTION"),
                [Game + @"old\nvngx_dlssg.dll"] = new RuntimeFileVersion(3, 6, 0, 0,
                    "NVIDIA DLSS-G - DVS PRODUCTION"),
                [NgxCopy] = new RuntimeFileVersion(310, 9, 16, 0, "NVIDIA DLSS-G MFGLW - DVS PRODUCTION"),
                [Game + "libxess_fg.dll"] = new RuntimeFileVersion(1, 3, 1, 78, "XeSS Frame Generation SDK"),
                [Game + @"newer\libxess_fg.dll"] = new RuntimeFileVersion(1, 4, 0, 12, "XeSS Frame Generation SDK"),
                // The driver's runtime carries the SDK's description, so only its name tells it apart.
                [IntelDriverRuntime] = new RuntimeFileVersion(1, 3, 3, 93, "XeSS Frame Generation SDK"),
                [Game + "nvngx_dlss.dll"] = new RuntimeFileVersion(310, 7, 130, 0, "NVIDIA DLSS - DVS PRODUCTION"),
                [Game + "libxess.dll"] = new RuntimeFileVersion(2, 0, 2, 68, "XeSS SDK"),
                [Game + "amd_fidelityfx_upscaler_dx12.dll"] = new RuntimeFileVersion(4, 0, 2, 44888,
                    "FFXDLL_UPSCALER_VERSION_API AMD FidelityFX Upscaler Library"),
            };

        [TestMethod]
        public void Find_ReadsTheVersionOfTheLoadedFrameGenerationLibraries()
        {
            var found = FrameGenerationRuntimeModuleClassifier.Find(Modules(
                Game + "nvngx_dlssg.dll", Game + "libxess_fg.dll", Game + "nvngx_dlss.dll",
                Game + "libxess.dll", Game + "amd_fidelityfx_upscaler_dx12.dll"), Read);

            Assert.AreEqual(2, found.Count, "Upscaler libraries and FSR are not frame generation runtimes.");
            Assert.AreEqual("310.2.1", found[EFrameGenerationRuntime.DlssFrameGeneration].ToDisplayString());
            Assert.AreEqual("1.3.1", found[EFrameGenerationRuntime.XessFrameGeneration].ToDisplayString());
            Assert.AreEqual("libxess_fg.dll", found[EFrameGenerationRuntime.XessFrameGeneration].Module,
                "The log names the module the version came from.");
        }

        [TestMethod]
        public void Find_NamesTheDriverRuntimeThatRunsTheXessContext()
        {
            // Driver 9175 takes the context of the 1.3.1.78 SDK (Dying Light: The Beast,
            // CapFrameX.Test's cfx_d3d12): the driver's copy stays loaded and runs it.
            foreach (var modules in new[]
                     {
                         Modules(Game + "libxess_fg.dll", IntelDriverRuntime),
                         Modules(IntelDriverRuntime, Game + "libxess_fg.dll")
                     })
            {
                var found = FrameGenerationRuntimeModuleClassifier.Find(modules, Read);

                RuntimeFileVersion xess = found[EFrameGenerationRuntime.XessFrameGeneration];
                Assert.AreEqual("1.3.3", xess.ToDisplayString(), "Whatever the module order.");
                Assert.AreEqual("igxess_fg.dll", xess.Module);
            }
            Assert.IsTrue(FrameGenerationRuntimeModuleClassifier.TryClassify(
                new HookModuleRecord("IGXESS_FG.DLL", IntelDriverRuntime), out var runtime, out bool driver));
            Assert.AreEqual(EFrameGenerationRuntime.XessFrameGeneration, runtime);
            Assert.IsTrue(driver);
            Assert.IsTrue(FrameGenerationRuntimeModuleClassifier.TryClassify(
                new HookModuleRecord("libxess_fg.dll", Game + "libxess_fg.dll"), out _, out driver));
            Assert.IsFalse(driver);
        }

        [TestMethod]
        public void Find_ALoadedDriverRuntimeComesBeforeANewerGameLibrary()
        {
            // The driver decides whether it runs the context, not the versions: a loaded copy of
            // its runtime runs it, as the SDK unloads it at once when it keeps the context.
            var found = FrameGenerationRuntimeModuleClassifier.Find(Modules(
                Game + @"newer\libxess_fg.dll", IntelDriverRuntime, Game + "libxess_fg.dll"), Read);

            Assert.AreEqual("1.3.3", found[EFrameGenerationRuntime.XessFrameGeneration].ToDisplayString());
            Assert.AreEqual("1.4.0", FrameGenerationRuntimeModuleClassifier.Find(Modules(
                    Game + "libxess_fg.dll", Game + @"newer\libxess_fg.dll"), Read)
                [EFrameGenerationRuntime.XessFrameGeneration].ToDisplayString(),
                "Without the driver's runtime the newest library of the game counts, as before.");
        }

        [TestMethod]
        public void Find_KnowsTheCopyNgxLoadsUnderAnotherName()
        {
            // NGX loads a downloaded or overridden DLSS-G as <id>.bin from its model folder: the
            // version on screen has to be the one that runs, not the one the game shipped.
            var found = FrameGenerationRuntimeModuleClassifier.Find(Modules(NgxCopy), Read);

            Assert.AreEqual("310.9.16", found[EFrameGenerationRuntime.DlssFrameGeneration].ToDisplayString());
            Assert.IsTrue(FrameGenerationRuntimeModuleClassifier.TryClassify(
                new HookModuleRecord("1B0_E658700.bin",
                    @"C:/ProgramData/NVIDIA/NGX/models/dlssg_override/versions/1/files/1B0_E658700.bin"),
                out var runtime));
            Assert.AreEqual(EFrameGenerationRuntime.DlssFrameGeneration, runtime);
            Assert.IsFalse(FrameGenerationRuntimeModuleClassifier.TryClassify(
                new HookModuleRecord("160_E658700.bin",
                    @"C:\ProgramData\NVIDIA\NGX\models\dlss\versions\20318480\files\160_E658700.bin"), out _),
                "The upscaler's copy is not frame generation.");
        }

        [TestMethod]
        public void Find_TakesTheNewestOfSeveralCopies()
        {
            var found = FrameGenerationRuntimeModuleClassifier.Find(Modules(
                Game + @"old\nvngx_dlssg.dll", NgxCopy, Game + "nvngx_dlssg.dll"), Read);

            Assert.AreEqual("310.9.16", found[EFrameGenerationRuntime.DlssFrameGeneration].ToDisplayString());
        }

        [TestMethod]
        public void Find_SkipsADlssFrameGenerationLibraryThatIsNotNvidias()
        {
            // A replacement that maps DLSS-G onto another technique has the name, not the version.
            string replacement = Game + @"mod\nvngx_dlssg.dll";
            RuntimeFileVersion? ReadWithReplacement(string path)
                => path == replacement ? new RuntimeFileVersion(0, 9, 0, 0, "dlssg to fsr3")
                    : path == Game + @"unversioned\nvngx_dlssg.dll" ? new RuntimeFileVersion(0, 0, 0, 0, null)
                    : Read(path);

            Assert.AreEqual(0, FrameGenerationRuntimeModuleClassifier.Find(Modules(replacement,
                Game + @"unversioned\nvngx_dlssg.dll", Game + @"missing\nvngx_dlssg.dll"),
                ReadWithReplacement).Count);
            Assert.AreEqual(0, FrameGenerationRuntimeModuleClassifier.Find(null, Read).Count);
        }

        [TestMethod]
        public void Versions_AnswerFromTheLastScanAndNeverScanOnTheCallingThread()
        {
            var rig = new Rig(Modules(Game + "nvngx_dlssg.dll"));

            Assert.IsNull(rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration),
                "The first question starts the scan and does not wait for it.");
            Assert.AreEqual(0, rig.Scans);
            Assert.IsNull(rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration));
            Assert.AreEqual(1, rig.Pending.Count, "One scan per process at a time.");

            rig.RunPending();
            Assert.AreEqual("310.2.1", rig.Versions.GetVersion(4711,
                EFrameGenerationRuntime.DlssFrameGeneration)?.ToDisplayString());
            Assert.AreEqual(1, rig.Scans);
            Assert.IsNull(rig.Versions.GetVersion(0, EFrameGenerationRuntime.DlssFrameGeneration));
        }

        [TestMethod]
        public void Versions_LookAgainSoonWhileTheLibraryOfAnActiveTechniqueIsMissing()
        {
            var rig = new Rig(Modules(Game + "nvngx_dlssg.dll"));
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.XessFrameGeneration);
            rig.RunPending();

            rig.Now += FrameGenerationRuntimeModuleVersions.RetryIntervalMs - 1;
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.XessFrameGeneration);
            Assert.AreEqual(0, rig.Pending.Count);

            rig.Modules = Modules(Game + "nvngx_dlssg.dll", Game + "libxess_fg.dll");
            rig.Now += 1;
            Assert.IsNull(rig.Versions.GetVersion(4711, EFrameGenerationRuntime.XessFrameGeneration));
            rig.RunPending();
            Assert.AreEqual("1.3.1", rig.Versions.GetVersion(4711,
                EFrameGenerationRuntime.XessFrameGeneration)?.ToDisplayString());
        }

        [TestMethod]
        public void Versions_NoticeAReloadedLibraryAfterAWhile()
        {
            var rig = new Rig(Modules(Game + "nvngx_dlssg.dll"));
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration);
            rig.RunPending();

            rig.Modules = Modules(NgxCopy);
            rig.Now += FrameGenerationRuntimeModuleVersions.RescanIntervalMs - 1;
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration);
            Assert.AreEqual(0, rig.Pending.Count, "A loaded library keeps its version.");

            rig.Now += 1;
            Assert.AreEqual("310.2.1", rig.Versions.GetVersion(4711,
                EFrameGenerationRuntime.DlssFrameGeneration)?.ToDisplayString(),
                "The former answer stands until the new scan has finished.");
            rig.RunPending();
            Assert.AreEqual("310.9.16", rig.Versions.GetVersion(4711,
                EFrameGenerationRuntime.DlssFrameGeneration)?.ToDisplayString());
        }

        [TestMethod]
        public void Versions_BelongToOneProcess()
        {
            var rig = new Rig(Modules(Game + "nvngx_dlssg.dll"));
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration);
            rig.RunPending();

            rig.Modules = Modules(NgxCopy);
            Assert.IsNull(rig.Versions.GetVersion(4712, EFrameGenerationRuntime.DlssFrameGeneration),
                "Another game does not inherit the version of the former one.");
            rig.RunPending();
            Assert.AreEqual("310.9.16", rig.Versions.GetVersion(4712,
                EFrameGenerationRuntime.DlssFrameGeneration)?.ToDisplayString());
        }

        [TestMethod]
        public void Versions_AScanOfTheFormerTargetIsDiscarded()
        {
            var rig = new Rig(Modules(Game + "nvngx_dlssg.dll"));
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration);
            var scanOfFormerTarget = rig.Pending.Dequeue();

            rig.Modules = Modules(NgxCopy);
            rig.Versions.GetVersion(4712, EFrameGenerationRuntime.DlssFrameGeneration);
            rig.Modules = Modules(Game + "nvngx_dlssg.dll");
            scanOfFormerTarget();
            Assert.IsNull(rig.Versions.GetVersion(4712, EFrameGenerationRuntime.DlssFrameGeneration));

            rig.Modules = Modules(NgxCopy);
            rig.RunPending();
            Assert.AreEqual("310.9.16", rig.Versions.GetVersion(4712,
                EFrameGenerationRuntime.DlssFrameGeneration)?.ToDisplayString());
        }

        [TestMethod]
        public void Versions_AnUnreadableModuleListKeepsTheLastAnswer()
        {
            // A process that denies the snapshot says nothing about its libraries.
            var rig = new Rig(Modules(Game + "nvngx_dlssg.dll"));
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration);
            rig.RunPending();

            rig.Modules = null;
            rig.Now += FrameGenerationRuntimeModuleVersions.RescanIntervalMs;
            rig.Versions.GetVersion(4711, EFrameGenerationRuntime.DlssFrameGeneration);
            rig.RunPending();

            Assert.AreEqual(2, rig.Scans);
            Assert.AreEqual("310.2.1", rig.Versions.GetVersion(4711,
                EFrameGenerationRuntime.DlssFrameGeneration)?.ToDisplayString());
        }

        private static RuntimeFileVersion? Read(string path)
            => Files.TryGetValue(path, out RuntimeFileVersion version) ? version : (RuntimeFileVersion?)null;

        private static IReadOnlyList<HookModuleRecord> Modules(params string[] paths)
        {
            var modules = new List<HookModuleRecord>
            {
                new HookModuleRecord("game.exe", Game + "game.exe"),
                new HookModuleRecord("d3d12.dll", @"C:\Windows\System32\d3d12.dll")
            };
            foreach (string path in paths)
                modules.Add(new HookModuleRecord(System.IO.Path.GetFileName(path), path));
            return modules;
        }

        private sealed class Rig
        {
            internal Rig(IReadOnlyList<HookModuleRecord> modules)
            {
                Modules = modules;
                Versions = new FrameGenerationRuntimeModuleVersions(_ =>
                {
                    Scans++;
                    return Modules;
                }, Read, () => Now, Pending.Enqueue);
            }

            internal FrameGenerationRuntimeModuleVersions Versions { get; }
            internal Queue<Action> Pending { get; } = new Queue<Action>();
            internal IReadOnlyList<HookModuleRecord> Modules { get; set; }
            internal long Now { get; set; } = 500_000;
            internal int Scans { get; private set; }

            internal void RunPending()
            {
                while (Pending.Count > 0) Pending.Dequeue()();
            }
        }
    }
}
