using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.View.OverlayDesignAuthoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    public class OverlayTileDocumentTest
    {
        private static readonly OsdTelemetrySource[] NoSources = Array.Empty<OsdTelemetrySource>();

        [TestMethod]
        public void EveryStarterProfileSurvivesTheTileDocumentRoundTrip()
        {
            foreach (var starter in OverlayDesignStarterCatalog.Profiles)
            {
                var original = OsdDesignDocument.FromJson(starter.DesignJson);
                var exchanged = JObject.Parse(OverlayTileDocument.ToJson(original).ToString());

                var rebuilt = OverlayTileDocument.FromJson(exchanged, original.Name, NoSources);

                Assert.AreEqual(original.ToJson(), rebuilt.ToJson(), $"Starter '{starter.PresetId}' changed in the tile document.");
            }
        }

        [TestMethod]
        public void EveryGalleryTemplateSurvivesTheTileDocumentRoundTrip()
        {
            foreach (var template in OsdDesignPresets.All)
            {
                var original = template.CreateDocument();
                var rebuilt = OverlayTileDocument.FromJson(OverlayTileDocument.ToJson(original), original.Name, NoSources);
                Assert.AreEqual(original.ToJson(), rebuilt.ToJson(), $"Template '{template.Id}' changed in the tile document.");
            }
        }

        [TestMethod]
        public void TilesWithoutFramesAreLaidOutInColumnsAndTheCanvasFitsThem()
        {
            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""width"": 320,
                ""tiles"": [
                    { ""kind"": ""metric"", ""key"": ""fps"", ""label"": ""FPS"", ""valueSize"": 36 },
                    { ""kind"": ""metric"", ""key"": ""frametime"", ""label"": ""Frame time"", ""unit"": ""ms"", ""digits"": 1 },
                    { ""kind"": ""chart"", ""columnSpan"": 2, ""frameHeight"": 120 }
                ]}"), "Layout", NoSources);

            Assert.IsTrue(document.IsCanvas, "A tile document defaults to the canvas layout.");
            var (fps, frametime, chart) = (document.Tiles[0], document.Tiles[1], document.Tiles[2]);
            Assert.AreEqual(0, fps.X); Assert.AreEqual(0, fps.Y);
            Assert.AreEqual(156, fps.FrameWidth, "Two columns of 320 px with an 8 px gap are 156 px wide.");
            Assert.AreEqual(164, frametime.X, "The second half-width tile fits beside the first.");
            Assert.AreEqual(0, frametime.Y);
            Assert.AreEqual(0, chart.X);
            Assert.AreEqual(Math.Max(fps.FrameHeight.Value, frametime.FrameHeight.Value) + 8, chart.Y);
            Assert.AreEqual(320, chart.FrameWidth);
            Assert.AreEqual("frametimes", chart.Key, "A chart defaults to the frame time series.");
            Assert.AreEqual("ms", chart.Unit);
            Assert.AreEqual(Math.Ceiling((chart.Y.Value + 120) / 8) * 8, document.CanvasHeight, "An omitted canvas height fits the tiles.");
            string json = document.ToJson();
            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson(), "The result must open in the editor unchanged.");
        }

        [TestMethod]
        public void ContainerChildrenAreSizedToTheContainerAndTheContainerGrows()
        {
            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""width"": 400, ""columns"": 1,
                ""tiles"": [ { ""kind"": ""container"", ""padding"": 10, ""children"": [
                    { ""kind"": ""text"", ""text"": ""GPU"" },
                    { ""kind"": ""metric"", ""key"": ""gpuTemp"", ""label"": ""Temp"" }
                ] } ]}"), "Card", NoSources);

            var card = document.Tiles[0];
            Assert.AreEqual(400, card.FrameWidth);
            foreach (var child in card.Children)
                Assert.IsTrue(child.X + child.FrameWidth <= 380 + 1e-7, "Children stay inside the container's inner width.");
            double childrenBottom = card.Children.Max(child => child.Y.Value + child.FrameHeight.Value);
            Assert.IsTrue(card.FrameHeight >= childrenBottom + 20, "The container's height includes its children and padding.");
            Assert.AreEqual("Text", card.Children[0].Label);
        }

        [TestMethod]
        public void UnknownFieldsAndKindsNameTheTileAndSuggestTheField()
        {
            var typo = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(
                JObject.Parse(@"{ ""tiles"": [ { ""kind"": ""metric"" }, { ""kind"": ""metric"", ""lable"": ""FPS"" } ] }"), "Typo", NoSources));
            StringAssert.Contains(typo.Message, "Tile 1");
            StringAssert.Contains(typo.Message, "'label'");

            var kind = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(
                JObject.Parse(@"{ ""tiles"": [ { ""kind"": ""sparkline"" } ] }"), "Kind", NoSources));
            StringAssert.Contains(kind.Message, "unknown kind 'sparkline'");

            var document = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(
                JObject.Parse(@"{ ""colour"": ""#FFFFFFFF"", ""tiles"": [ { ""kind"": ""metric"" } ] }"), "Field", NoSources));
            StringAssert.Contains(document.Message, "'colour'");
        }

        [TestMethod]
        public void EditorValidationErrorsNameTheDeepestFailingTile()
        {
            var top = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""tiles"": [ { ""kind"": ""metric"" }, { ""kind"": ""metric"", ""label"": ""Huge"", ""valueSize"": 200 } ] }"), "Range", NoSources));
            StringAssert.Contains(top.Message, "Tile 1 (metric 'Huge')");

            var nested = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""tiles"": [ { ""kind"": ""container"", ""children"": [
                    { ""kind"": ""text"", ""text"": ""ok"" },
                    { ""kind"": ""metric"", ""label"": ""Precise"", ""digits"": 9 } ] } ] }"), "Nested", NoSources));
            StringAssert.Contains(nested.Message, "Tile 0/1 (metric 'Precise')");
        }

        [TestMethod]
        public void SemanticKeysBindToThisMachinesReadingAndGroupKeysFillTheMembers()
        {
            var sources = new List<OsdTelemetrySource>
            {
                new OsdTelemetrySource { Key = "sensor:gpu%2Ftemp", Name = "GPU Core", Unit = "°C", Category = "Graphics", SemanticKey = "gpuTemp" }
            };
            for (int core = 0; core < 4; core++)
                sources.Add(new OsdTelemetrySource
                {
                    Key = "sensor:cpu%2Fclock%2F" + core, Name = "Core #" + core, Unit = "MHz", Category = "Processor",
                    DeviceName = "Test CPU", DeviceKey = "device:cpu", GroupKey = "core-clocks", GroupName = "Core clocks", GroupOrder = core
                });
            string family = OsdSourceGroup.Build(sources).Single().Key;

            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{ ""tiles"": [
                { ""kind"": ""metric"", ""key"": ""gpuTemp"" },
                { ""kind"": ""group"", ""groupKey"": """ + family + @""", ""columnSpan"": 2 } ] }"), "Bound", sources);

            Assert.AreEqual("sensor:gpu%2Ftemp", document.Tiles[0].Key, "A portable role binds to the unambiguous reading.");
            Assert.AreEqual("°C", document.Tiles[0].Unit);
            Assert.AreEqual("GPU Core", document.Tiles[0].Label, "An omitted label comes from the reading.");
            var group = document.Tiles[1];
            Assert.AreEqual(4, group.GroupMembers.Count);
            Assert.AreEqual("metric", group.GroupDisplay);
            Assert.IsTrue(group.FrameHeight > 24, "The group is sized for its members.");
            CollectionAssert.AreEqual(Array.Empty<string>(), OverlayTileDocument.FindUnboundKeys(document, sources).ToArray());
        }

        [TestMethod]
        public void UnboundKeysAreReportedPerTile()
        {
            var sources = new[] { new OsdTelemetrySource { Key = "fps-key", Name = "FPS", SemanticKey = "fps" } };
            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{ ""tiles"": [
                { ""kind"": ""metric"", ""key"": ""fps"" }, { ""kind"": ""bar"", ""key"": ""missing"", ""label"": ""Nothing"" } ] }"), "Keys", sources);

            var findings = OverlayTileDocument.FindUnboundKeys(document, sources);

            Assert.AreEqual(1, findings.Count);
            StringAssert.Contains(findings[0], "Tile 1 ('Nothing')");
            StringAssert.Contains(findings[0], "'missing'");
        }

        [TestMethod]
        public void EditOperationsApplyInOrderOnTheTileDocument()
        {
            var start = OverlayTileDocument.FromJson(JObject.Parse(@"{ ""width"": 320, ""tiles"": [
                { ""kind"": ""metric"", ""key"": ""fps"", ""label"": ""FPS"" },
                { ""kind"": ""metric"", ""key"": ""frametime"", ""label"": ""Frame time"" },
                { ""kind"": ""container"", ""columnSpan"": 2, ""children"": [ { ""kind"": ""text"", ""text"": ""A"" } ] } ] }"), "Edit", NoSources);

            var edited = OverlayTileDocument.ApplyOperations(OverlayTileDocument.ToJson(start), JArray.Parse(@"[
                { ""op"": ""setDocument"", ""values"": { ""accent"": ""#FF6A00FF"" } },
                { ""op"": ""updateTile"", ""path"": ""0"", ""values"": { ""valueSize"": 40, ""frameWidth"": null, ""frameHeight"": null } },
                { ""op"": ""addTile"", ""parent"": ""2"", ""tile"": { ""kind"": ""text"", ""text"": ""B"" } },
                { ""op"": ""moveTile"", ""path"": ""1"", ""index"": 0 },
                { ""op"": ""remove_tile"", ""path"": ""2/0"" }
            ]"));
            var document = OverlayTileDocument.FromJson(edited, "Edit", NoSources);

            Assert.AreEqual("#FF6A00FF", document.Accent);
            Assert.AreEqual("Frame time", document.Tiles[0].Label, "moveTile changed the layer order.");
            Assert.AreEqual(40, document.Tiles[1].ValueSize);
            Assert.IsTrue(document.Tiles[1].FrameHeight > start.Tiles[0].FrameHeight, "A null frame is recomputed for the larger value.");
            Assert.AreEqual(1, document.Tiles[2].Children.Count);
            Assert.AreEqual("B", document.Tiles[2].Children[0].Text);
        }

        [TestMethod]
        public void InvalidOperationsNameTheirIndex()
        {
            var document = OverlayTileDocument.ToJson(OverlayTileDocument.FromJson(
                JObject.Parse(@"{ ""tiles"": [ { ""kind"": ""metric"" } ] }"), "Ops", NoSources));

            var path = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.ApplyOperations(document,
                JArray.Parse(@"[ { ""op"": ""setDocument"", ""values"": { ""gap"": 4 } }, { ""op"": ""removeTile"", ""path"": ""5"" } ]")));
            StringAssert.Contains(path.Message, "Operation 1 (removeTile)");
            StringAssert.Contains(path.Message, "'5'");

            var parent = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.ApplyOperations(document,
                JArray.Parse(@"[ { ""op"": ""addTile"", ""parent"": ""0"", ""tile"": { ""kind"": ""text"" } } ]")));
            StringAssert.Contains(parent.Message, "not a container");

            var name = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.ApplyOperations(document,
                JArray.Parse(@"[ { ""op"": ""setDocument"", ""values"": { ""name"": ""Other"" } } ]")));
            StringAssert.Contains(name.Message, "cfx_rename_tile_profile");
        }

        [TestMethod]
        public void ExplicitFramesBeyondTheCanvasGrowItWithAWarning()
        {
            var warnings = new List<string>();
            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{ ""width"": 300, ""canvasHeight"": 100, ""tiles"": [
                { ""kind"": ""metric"", ""x"": 0, ""y"": 96, ""frameWidth"": 200, ""frameHeight"": 80 } ] }"), "Grow", NoSources, warnings);

            Assert.AreEqual(176, document.CanvasHeight);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "canvas height grew");
        }
    }
}
