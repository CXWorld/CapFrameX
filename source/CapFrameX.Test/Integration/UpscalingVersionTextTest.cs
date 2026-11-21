using System.Globalization;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// What the "Upscaler version", "Frame generation version" and "Frame generation factor"
    /// entries read, and the rule behind them: only a technique that is proven to run is named,
    /// and its factor is the measured one.
    /// </summary>
    [TestClass]
    public class UpscalingVersionTextTest
    {
        private const int Unknown = 0;
        private const int Dlss = 1;
        private const int Xess = 2;
        private const int Fsr = 3;
        private const int Inactive = 1;
        private const int Active = 2;
        private const ulong Now = 1_000_000;

        private static readonly RuntimeFileVersion DlssG = new RuntimeFileVersion(310, 7, 130, 0,
            "NVIDIA DLSS-G MFGLW - DVS PRODUCTION");
        private static readonly RuntimeFileVersion XessFg = new RuntimeFileVersion(1, 3, 1, 78,
            "XeSS Frame Generation SDK");

        [TestMethod]
        public void Upscaler_NamesTheVersionOfTheDispatchedContext()
        {
            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 200), Now));
        }

        [TestMethod]
        public void Upscaler_MarksTheDefaultVersionOfTheLibrary()
        {
            // amd_fidelityfx_dx12.dll 1.0.x cannot be asked for the provider of a context.
            Assert.AreEqual("FSR 3.1.2 (DLL)", UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.LibraryDefault, "3.1.2", Now - 200), Now));
        }

        [TestMethod]
        public void Upscaler_ALibraryThatIsNotDispatchedNamesNothing()
        {
            // Loaded into the game is not in use: DLSS or XeSS may be the selected upscaler.
            Assert.IsNull(UpscalingVersionText.FsrInUse(default, Now));
            Assert.IsNull(UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", 0), Now),
                "A version without a dispatch was never used.");
            Assert.IsNull(UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 3001), Now),
                "The game stopped dispatching: another upscaler was selected.");
            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 3000), Now));
            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", Now + 5), Now),
                "The hook stamped between the two clock reads.");
        }

        [TestMethod]
        public void Upscaler_AUseWithoutAVersionNamesNothing()
        {
            Assert.IsNull(UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.None, null, Now - 200), Now));
            Assert.IsNull(UpscalingVersionText.FsrInUse(
                Version(HookFidelityFxVersionSource.Context, "", Now - 200), Now));
        }

        [TestMethod]
        public void Upscaler_NamesTheLibraryThatEvaluates()
        {
            Assert.AreEqual("DLSS 310.7.130", UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(Now - 200, 310, 7, 130)), Now));
            Assert.AreEqual("DLSS RR 310.9.16", UpscalingVersionText.Upscaler(default,
                Uses(rayReconstruction: Use(Now - 200, 310, 9, 16)), Now),
                "Ray Reconstruction upscales in place of Super Resolution.");
            Assert.AreEqual("XeSS 2.0.2", UpscalingVersionText.Upscaler(default,
                Uses(xess: Use(Now - 200, 2, 0, 2, 68)), Now),
                "Three components: the fourth is a build counter.");
            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.Upscaler(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 200), default, Now));
        }

        [TestMethod]
        public void Upscaler_ALibraryThatDoesNotEvaluateNamesNothing()
        {
            // A game loads the library of every upscaler it ships; NGX loads Ray Reconstruction
            // beside Super Resolution. Only an evaluation within the window is use.
            Assert.IsNull(UpscalingVersionText.Upscaler(default, default, Now));
            Assert.IsNull(UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(0, 310, 7, 130)), Now),
                "A version without an evaluation was never used.");
            Assert.IsNull(UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(Now - 3001, 310, 7, 130), xess: Use(Now - 60000, 2, 0, 2)), Now),
                "The game stopped evaluating: another upscaler or none was selected.");
            Assert.AreEqual("DLSS 310.7.130", UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(Now - 3000, 310, 7, 130)), Now));
            Assert.AreEqual("DLSS 310.7.130", UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(Now + 5, 310, 7, 130)), Now),
                "The hook stamped between the two clock reads.");
            Assert.IsNull(UpscalingVersionText.Upscaler(default,
                Uses(xess: Use(Now - 200, 0, 0, 0)), Now),
                "A library without a version resource has no version to name.");
        }

        [TestMethod]
        public void Upscaler_AfterASwitchTheOneThatRanLastIsNamed()
        {
            // The former upscaler is still inside the window when the new one starts.
            var fsr = Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 100);
            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.Upscaler(fsr,
                Uses(dlss: Use(Now - 1500, 310, 7, 130)), Now));
            Assert.AreEqual("DLSS 310.7.130", UpscalingVersionText.Upscaler(
                Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 1500),
                Uses(dlss: Use(Now - 100, 310, 7, 130)), Now));
            Assert.AreEqual("XeSS 2.0.2", UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(Now - 2500, 310, 7, 130), xess: Use(Now - 100, 2, 0, 2)), Now));
            Assert.AreEqual("DLSS 310.7.130", UpscalingVersionText.Upscaler(default,
                Uses(dlss: Use(Now - 100, 310, 7, 130),
                    rayReconstruction: Use(Now - 1200, 310, 7, 130)), Now),
                "Ray Reconstruction was switched off.");
        }

        [TestMethod]
        public void Upscaler_TwoThatRunAtTheSameTimeDoNotAlternate()
        {
            // Both publish their time of use about four times per second, so which one is the
            // newer changes with every refresh. The order decides, not the few milliseconds.
            var fsrNewer = Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 20);
            var fsrOlder = Version(HookFidelityFxVersionSource.Context, "3.1.5", Now - 240);
            Assert.AreEqual("XeSS 2.0.2", UpscalingVersionText.Upscaler(fsrNewer,
                Uses(xess: Use(Now - 240, 2, 0, 2)), Now));
            Assert.AreEqual("XeSS 2.0.2", UpscalingVersionText.Upscaler(fsrOlder,
                Uses(xess: Use(Now - 20, 2, 0, 2)), Now));
            Assert.AreEqual("DLSS 310.7.130", UpscalingVersionText.Upscaler(fsrNewer,
                Uses(dlss: Use(Now - 1000, 310, 7, 130), xess: Use(Now - 500, 2, 0, 2)), Now));
            Assert.AreEqual("DLSS RR 310.9.16", UpscalingVersionText.Upscaler(fsrNewer,
                Uses(dlss: Use(Now - 20, 310, 7, 130),
                    rayReconstruction: Use(Now - 240, 310, 9, 16)), Now));

            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.Upscaler(fsrNewer,
                Uses(xess: Use(Now - 1021, 2, 0, 2)), Now),
                "More than a second behind is no longer at the same time.");
            Assert.AreEqual("FSR 3.1.5", UpscalingVersionText.Upscaler(fsrNewer,
                Uses(xess: Use(Now - 20, 0, 0, 0)), Now),
                "An upscaler without a version to name does not hide the one that has it.");
        }

        [TestMethod]
        public void FrameGeneration_NamesTheTechniqueThatIsSwitchedOn()
        {
            var fsr = Version(HookFidelityFxVersionSource.Context, "3.1.6", Now - 100);

            Assert.AreEqual("DLSS 310.7.130",
                UpscalingVersionText.FrameGeneration(Dlss, Active, default, Now, DlssG, null));
            Assert.AreEqual("XeSS 1.3.1",
                UpscalingVersionText.FrameGeneration(Xess, Active, default, Now, null, XessFg),
                "Three components: the fourth is a build counter.");
            Assert.AreEqual("FSR 3.1.6",
                UpscalingVersionText.FrameGeneration(Fsr, Active, fsr, Now, null, null));
        }

        [TestMethod]
        public void FrameGeneration_OnlyTheActiveTechniqueCounts()
        {
            // A game that ships all three loads all three; only the one that generates is named.
            var fsr = Version(HookFidelityFxVersionSource.Context, "3.1.6", Now - 100);

            Assert.AreEqual("DLSS 310.7.130",
                UpscalingVersionText.FrameGeneration(Dlss, Active, fsr, Now, DlssG, XessFg));
            Assert.AreEqual("XeSS 1.3.1",
                UpscalingVersionText.FrameGeneration(Xess, Active, fsr, Now, DlssG, XessFg));
            Assert.AreEqual("FSR 3.1.6",
                UpscalingVersionText.FrameGeneration(Fsr, Active, fsr, Now, DlssG, XessFg));
        }

        [TestMethod]
        public void FrameGeneration_SwitchedOffOrUnknownNamesNothing()
        {
            var fsr = Version(HookFidelityFxVersionSource.Context, "3.1.6", Now - 100);

            foreach (int activity in new[] { Unknown, Inactive, 3 })
            {
                Assert.IsNull(UpscalingVersionText.FrameGeneration(Dlss, activity, fsr, Now, DlssG, XessFg));
                Assert.IsNull(UpscalingVersionText.FrameGeneration(Xess, activity, fsr, Now, DlssG, XessFg));
                Assert.IsNull(UpscalingVersionText.FrameGeneration(Fsr, activity, fsr, Now, DlssG, XessFg));
            }
            Assert.IsNull(UpscalingVersionText.FrameGeneration(Unknown, Active, fsr, Now, DlssG, XessFg));
        }

        [TestMethod]
        public void FrameGeneration_AnActiveTechniqueWithoutAKnownVersionNamesNothing()
        {
            Assert.IsNull(UpscalingVersionText.FrameGeneration(Dlss, Active, default, Now, null, XessFg),
                "The library of the active technique was not found; another one is not its version.");
            Assert.IsNull(UpscalingVersionText.FrameGeneration(Xess, Active, default, Now, DlssG, null));
            Assert.IsNull(UpscalingVersionText.FrameGeneration(Dlss, Active, default, Now,
                new RuntimeFileVersion(0, 0, 0, 0, null), null));
            Assert.IsNull(UpscalingVersionText.FrameGeneration(Fsr, Active,
                Version(HookFidelityFxVersionSource.Context, "3.1.6", Now - 5000), Now, null, null),
                "Frame generation is on, but its context was not dispatched for seconds.");
        }

        [TestMethod]
        public void Factor_IsAWholeFactorWhereTheMeasurementIsOne()
        {
            Assert.AreEqual("2x", UpscalingVersionText.FrameGenerationFactor(2.0));
            Assert.AreEqual("3x", UpscalingVersionText.FrameGenerationFactor(3.0));
            Assert.AreEqual("4x", UpscalingVersionText.FrameGenerationFactor(4.0));
            Assert.AreEqual("6x", UpscalingVersionText.FrameGenerationFactor(6.0));

            // Both counters are read a frame apart: 241 displayed frames over 120 rendered ones.
            Assert.AreEqual("2x", UpscalingVersionText.FrameGenerationFactor(241.0 / 120.0));
            Assert.AreEqual("2x", UpscalingVersionText.FrameGenerationFactor(1.9));
            Assert.AreEqual("3x", UpscalingVersionText.FrameGenerationFactor(2.85));
            Assert.AreEqual("4x", UpscalingVersionText.FrameGenerationFactor(3.8));
            Assert.AreEqual("4x", UpscalingVersionText.FrameGenerationFactor(4.2));
        }

        [TestMethod]
        public void Factor_BetweenTwoWholeOnesKeepsADecimal()
        {
            // A dynamic factor, or the seconds after a switch.
            CultureInfo culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("2.5x", UpscalingVersionText.FrameGenerationFactor(2.5),
                    "A decimal point in every language.");
                Assert.AreEqual("2.2x", UpscalingVersionText.FrameGenerationFactor(2.2));
                Assert.AreEqual("3.5x", UpscalingVersionText.FrameGenerationFactor(3.5));
                Assert.AreEqual("3.7x", UpscalingVersionText.FrameGenerationFactor(3.7));
                Assert.AreEqual("1.4x", UpscalingVersionText.FrameGenerationFactor(1.4));
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }
        }

        [TestMethod]
        public void Factor_WithoutGeneratedFramesIsOneTimes()
        {
            Assert.AreEqual("1x", UpscalingVersionText.FrameGenerationFactor(1.0));
            Assert.AreEqual("1x", UpscalingVersionText.FrameGenerationFactor(1.04));
            Assert.AreEqual("1x", UpscalingVersionText.FrameGenerationFactor(0.9),
                "Fewer displayed than rendered frames multiply nothing.");

            Assert.IsNull(UpscalingVersionText.FrameGenerationFactor(0));
            Assert.IsNull(UpscalingVersionText.FrameGenerationFactor(-2));
            Assert.IsNull(UpscalingVersionText.FrameGenerationFactor(double.NaN));
            Assert.IsNull(UpscalingVersionText.FrameGenerationFactor(double.PositiveInfinity));
        }

        private static HookFidelityFxProviderVersion Version(HookFidelityFxVersionSource source,
            string name, ulong lastUseTickMs)
            => new HookFidelityFxProviderVersion(source, name, 1, lastUseTickMs);

        private static HookUpscalerRuntimeUse Use(ulong lastUseTickMs, int major, int minor,
            int build, int revision = 0)
            => new HookUpscalerRuntimeUse(lastUseTickMs,
                new RuntimeFileVersion(major, minor, build, revision, null));

        private static HookUpscalerUses Uses(HookUpscalerRuntimeUse dlss = default,
            HookUpscalerRuntimeUse rayReconstruction = default, HookUpscalerRuntimeUse xess = default)
            => new HookUpscalerUses(dlss, rayReconstruction, xess);
    }
}
