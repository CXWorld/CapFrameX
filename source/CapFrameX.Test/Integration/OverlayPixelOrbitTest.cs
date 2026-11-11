using System;
using System.IO;
using System.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayPixelOrbitTest
    {
        [TestMethod]
        public void EditorRoundTripsThePixelShiftAndOmitsItWhenDisabled()
        {
            var document = OsdDesignDocument.CreatePreset("minimal");
            string plain = document.ToJson();
            Assert.IsNull(JObject.Parse(plain)["pixelShift"], "Designs without an orbit keep their exact exports.");

            document.PixelShiftEnabled = true;
            document.PixelShiftInterval = 240;
            document.PixelShiftRadius = 4;
            string json = document.ToJson();
            Assert.AreEqual(240, (int)JObject.Parse(json)["pixelShift"]["intervalSeconds"]);
            Assert.AreEqual(4, (int)JObject.Parse(json)["pixelShift"]["radius"]);

            var reopened = OsdDesignDocument.FromJson(json);
            Assert.IsTrue(reopened.PixelShiftEnabled);
            Assert.AreEqual(240, reopened.PixelShiftInterval);
            Assert.AreEqual(4, reopened.PixelShiftRadius);
            Assert.AreEqual(json, reopened.ToJson());

            reopened.PixelShiftEnabled = false;
            Assert.IsNull(JObject.Parse(reopened.ToJson())["pixelShift"]);
        }

        [TestMethod]
        [DataRow(9, 6)]
        [DataRow(3601, 6)]
        [DataRow(180, 0)]
        [DataRow(180, 33)]
        public void EditorRejectsOrbitParametersOutsideTheHostRange(int interval, int radius)
        {
            var document = OsdDesignDocument.CreatePreset("minimal");
            document.PixelShiftEnabled = true;
            document.PixelShiftInterval = interval;
            document.PixelShiftRadius = radius;
            Assert.Throws<FormatException>(() => document.ToJson());
            // Disabled values are never exported and therefore never validated.
            document.PixelShiftEnabled = false;
            document.ToJson();
        }

        [TestMethod]
        public void RuntimeReadsTheRequestAndKeepsItOutOfTheRendererScene()
        {
            var design = OverlayRuntimeDesign.Parse("oled", "OLED", OrbitDesign(240, 4));
            Assert.AreEqual(new OverlayPixelShift(240, 4), design.PixelShift);
            Assert.IsNull(JObject.Parse(design.TemplateJson)["pixelShift"], "The renderers place the scene; the host owns the orbit.");
            Assert.IsNull(OverlayRuntimeDesign.Parse("plain", "Plain", OsdDesignDocument.CreatePreset("minimal").ToJson()).PixelShift);

            var clamped = JObject.Parse(OrbitDesign(240, 4));
            clamped["pixelShift"]["intervalSeconds"] = 1;
            clamped["pixelShift"]["radius"] = 500;
            Assert.AreEqual(new OverlayPixelShift(OsdPixelOrbit.MinimumIntervalSeconds, OsdPixelOrbit.MaximumRadius),
                OverlayRuntimeDesign.Parse("clamped", "Clamped", clamped.ToString()).PixelShift);
        }

        [TestMethod]
        public void SourceBindingRetainsTheDesignsOrbit()
        {
            var document = OsdDesignDocument.CreatePreset("minimal");
            document.PixelShiftEnabled = true;
            string key = document.EnumerateTiles().First(tile => tile.Kind == "metric").Key;
            var design = OverlayRuntimeDesign.Parse("bound", "Bound", document.ToJson());
            var catalog = new[] { new OverlayTelemetrySource { Id = "metric/bound", Name = "Bound", Unit = "FPS", SemanticKey = key } };

            var bound = OverlayRuntimeSourceBinder.Bind(design, catalog);
            Assert.AreNotSame(design, bound, "The fixture must exercise an actual rebinding.");
            CollectionAssert.Contains(bound.MetricKeys.ToArray(), "metric/bound");
            Assert.AreEqual(design.PixelShift, bound.PixelShift);
        }

        [TestMethod]
        public void DesignRequestWinsAndTheGlobalOptionCoversEveryOtherDesign()
        {
            long now = 5 * 60_000L;
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            config.Object.OsdPixelOrbitEnabled = false;
            config.Object.OsdPixelOrbitInterval = 60;
            config.Object.OsdPixelOrbitRadius = 6;
            OverlayRuntimeDesign active = null;
            var resolver = new OverlayPixelOrbitResolver(config.Object, () => active, () => now);

            Assert.IsNull(resolver.Current);
            Assert.AreEqual((30, 40), resolver.Apply(30, 40), "Off unless the option or the design asks for it.");

            config.Object.OsdPixelOrbitEnabled = true;
            Assert.AreEqual(new OverlayPixelShift(60, 6), resolver.Current);
            Assert.AreEqual((35, 40), resolver.Apply(30, 40), "Five one-minute steps into the global orbit.");

            active = OverlayRuntimeDesign.Parse("plain", "Plain", OsdDesignDocument.CreatePreset("minimal").ToJson());
            Assert.AreEqual(new OverlayPixelShift(60, 6), resolver.Current, "A design without a request follows the option.");

            active = OverlayRuntimeDesign.Parse("oled", "OLED", OrbitDesign(150, 2));
            Assert.AreEqual(new OverlayPixelShift(150, 2), resolver.Current);
            Assert.AreEqual((32, 40), resolver.Apply(30, 40), "Two 150 s steps into the design's own orbit.");

            config.Object.OsdPixelOrbitEnabled = false;
            Assert.AreEqual(new OverlayPixelShift(150, 2), resolver.Current, "The design's request does not depend on the option.");
        }

        [TestMethod]
        public void OledTemplateOpensInTheDesignerAndCarriesItsOrbit()
        {
            string json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "overlay-templates", "OLED-Minimal.json"));
            var document = OsdDesignDocument.FromJson(json);
            Assert.AreEqual("OLED Minimal", document.Name);
            Assert.IsTrue(document.PixelShiftEnabled);
            Assert.AreEqual(OsdPixelOrbit.DefaultIntervalSeconds, document.PixelShiftInterval);
            Assert.AreEqual(OsdPixelOrbit.DefaultRadius, document.PixelShiftRadius);
            // OLED rules: no panels, borders or images, nothing brighter than the dimmed values.
            Assert.AreEqual("#00000000", document.TileColor);
            Assert.IsFalse(document.EnumerateTiles().Any(tile => tile.Kind == "image" || tile.Kind == "text" || tile.Kind == "container"));
            Assert.IsTrue(document.EnumerateTiles().All(tile => tile.BackgroundColor == "#00000000" && (tile.BorderWidth ?? 0) == 0));
            Assert.IsTrue(document.EnumerateTiles().Where(tile => tile.Kind == "chart").All(tile => tile.ShowBackground == false && tile.ShowBorder == false));

            var design = OverlayRuntimeDesign.Parse("oled", document.Name, json);
            Assert.AreEqual(new OverlayPixelShift(OsdPixelOrbit.DefaultIntervalSeconds, OsdPixelOrbit.DefaultRadius), design.PixelShift);
            CollectionAssert.AreEquivalent(new[] { "fps1pctLow", "fpsCurrent", "frametimeCurrent" }, design.MetricKeys.ToArray());
            Assert.IsTrue(design.NeedsFrametimes);
        }

        private static string OrbitDesign(int interval, int radius)
        {
            var document = OsdDesignDocument.CreatePreset("minimal");
            document.PixelShiftEnabled = true;
            document.PixelShiftInterval = interval;
            document.PixelShiftRadius = radius;
            return document.ToJson();
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CapFrameX.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory, "Could not find CapFrameX.sln above the test output.");
            return directory.FullName;
        }
    }
}
