using CapFrameX.Contracts.Overlay;
using CapFrameX.Mcp;
using CapFrameX.Mcp.Tools;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Test.Mcp
{
    [TestClass]
    public class TileProfileToolsTest
    {
        private const string Document = @"{""name"":""Test"",""layout"":""canvas"",""width"":320,""tiles"":[
            {""path"":""0"",""kind"":""metric"",""label"":""FPS"",""key"":""fps"",""x"":0,""y"":0,""frameWidth"":156,""frameHeight"":104},
            {""path"":""1"",""kind"":""container"",""label"":""Card"",""x"":164,""y"":0,""frameWidth"":156,""frameHeight"":80,
             ""children"":[{""path"":""1/0"",""kind"":""text"",""label"":""Text"",""text"":""Hi"",""x"":0,""y"":0,""frameWidth"":40,""frameHeight"":40}]}]}";

        [TestMethod]
        public void TheRegistryPublishesEveryTileToolWithUsableSchemas()
        {
            var registry = CreateRegistry(new Mock<IOverlayDesignAuthoringService>());

            var names = registry.GetAll().Select(tool => tool.Name).Where(name => name.Contains("tile")).OrderBy(name => name).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "cfx_activate_tile_profile", "cfx_create_tile_profile", "cfx_deactivate_tile_profile", "cfx_delete_tile_profile",
                "cfx_edit_tile_profile", "cfx_get_tile_design_guide", "cfx_get_tile_profile", "cfx_list_tile_profiles",
                "cfx_list_tile_sources", "cfx_list_tile_templates", "cfx_open_tile_editor", "cfx_rename_tile_profile",
                "cfx_render_tile_profile", "cfx_update_tile_profile"
            }, names);
            Assert.IsTrue(registry.TryGet("cfx_create_tile_profile", out var create));
            Assert.AreEqual("object", (string)create.InputSchema["properties"]["document"]["type"]);
            Assert.IsTrue(registry.TryGet("cfx_edit_tile_profile", out var edit));
            var operations = edit.InputSchema["properties"]["operations"];
            Assert.AreEqual("array", (string)operations["type"]);
            CollectionAssert.Contains(operations["items"]["properties"]["op"]["enum"].Values<string>().ToArray(), "updateTile");
            StringAssert.Contains((string)operations["description"], "applied in order");
        }

        [TestMethod]
        public async Task OperationsSentAsJsonTextAreParsedAndWritesReturnAnOutline()
        {
            var authoring = new Mock<IOverlayDesignAuthoringService>();
            string received = null;
            authoring.Setup(service => service.EditProfileAsync("Test", It.IsAny<string>()))
                .Callback<string, string>((_, operations) => received = operations)
                .ReturnsAsync(Content());
            var registry = CreateRegistry(authoring);
            registry.TryGet("cfx_edit_tile_profile", out var edit);

            var result = (JObject)await registry.InvokeAsync(edit, new JObject
            {
                ["profile"] = "Test",
                ["operations"] = "[{\"op\":\"removeTile\",\"path\":\"0\"}]"
            });

            Assert.AreEqual("removeTile", (string)JArray.Parse(received)[0]["op"]);
            Assert.IsNull(result["document"], "Writes return the outline unless the document is requested.");
            var tiles = (JArray)result["outline"]["tiles"];
            Assert.AreEqual(3, tiles.Count, "The outline lists nested tiles too.");
            Assert.AreEqual("1/0", (string)tiles[2]["path"]);
            Assert.AreEqual("0, 0, 156, 104", (string)tiles[0]["frame"]);
            Assert.AreEqual("fps", (string)tiles[0]["key"]);
            Assert.AreEqual(320, (int)result["outline"]["width"]);
            Assert.AreEqual("A warning", (string)result["warnings"][0]);
            Assert.AreEqual("abc", (string)result["profile"]["id"]);
        }

        [TestMethod]
        public async Task RenderingReturnsTheImageAndItsDescription()
        {
            var authoring = new Mock<IOverlayDesignAuthoringService>();
            authoring.Setup(service => service.RenderPreviewAsync("Test", null, true)).ReturnsAsync(new OverlayDesignPreview
            {
                PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }, Width = 320, Height = 200, Scale = 1, Values = "live",
                ProfileName = "Test",
                Tiles = new[] { new OverlayDesignPreviewTile { Path = "0", Kind = "metric", Label = "FPS", Width = 156, Height = 104 } }
            });
            var tool = new TileProfileTools(authoring.Object);

            var result = await tool.RenderTileProfile("Test");

            Assert.AreEqual(2, result.Content.Count);
            Assert.AreEqual("image", result.Content[0].Type);
            Assert.AreEqual("image/png", result.Content[0].MimeType);
            Assert.AreEqual(Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47 }), result.Content[0].Data);
            var description = JObject.Parse(result.Content[1].Text);
            Assert.AreEqual(320, (int)description["width"]);
            Assert.AreEqual("FPS", (string)description["tiles"][0]["label"]);
        }

        [TestMethod]
        public async Task CreationReportsAFailedActivationWithoutFailing()
        {
            var authoring = new Mock<IOverlayDesignAuthoringService>();
            authoring.Setup(service => service.CreateProfileAsync("New", null, "minimal", null)).ReturnsAsync(Content());
            authoring.Setup(service => service.ActivateProfileAsync("abc"))
                .ThrowsAsync(new OverlayDesignAuthoringException("Tile profiles need the CapFrameX in-game or hook-free renderer."));
            var tool = new TileProfileTools(authoring.Object);

            var result = await tool.CreateTileProfile("New", templateId: "minimal", activate: true);

            Assert.IsFalse((bool)result["activated"]);
            Assert.AreEqual(2, ((JArray)result["warnings"]).Count);
            StringAssert.Contains((string)result["warnings"][1], "not activated");
        }

        [TestMethod]
        public void SourcesAreFilteredByEveryTerm()
        {
            var authoring = new Mock<IOverlayDesignAuthoringService>();
            authoring.Setup(service => service.GetSourcesAsync()).ReturnsAsync(new OverlayDesignSourceCatalog
            {
                Sources = new[]
                {
                    new OverlayDesignSourceInfo { Key = "a", Name = "GPU Core", Category = "Graphics", Unit = "°C", IsAvailable = true },
                    new OverlayDesignSourceInfo { Key = "b", Name = "GPU Core", Category = "Graphics", Unit = "MHz", IsAvailable = true },
                    new OverlayDesignSourceInfo { Key = "c", Name = "GPU Hot Spot", Category = "Graphics", Unit = "°C", IsAvailable = false }
                }
            });
            var tool = new TileProfileTools(authoring.Object);

            var result = tool.ListTileSources("gpu °c").GetAwaiter().GetResult();

            Assert.AreEqual(1, (int)result["total"]);
            Assert.AreEqual("a", (string)result["sources"][0]["key"]);
            Assert.AreEqual(2, (int)tool.ListTileSources("°C", includeUnavailable: true).GetAwaiter().GetResult()["total"]);
        }

        private static McpToolRegistry CreateRegistry(Mock<IOverlayDesignAuthoringService> authoring)
        {
            var container = new Container();
            container.RegisterInstance(authoring.Object);
            container.Register<TileProfileTools>(Reuse.Singleton);
            return new McpToolRegistry(container, new[] { typeof(McpModule).Assembly });
        }

        private static OverlayDesignProfileContent Content() => new OverlayDesignProfileContent
        {
            Profile = new OverlayDesignProfileInfo { Id = "abc", Name = "Test", Layout = "canvas", TileCount = 3 },
            DocumentJson = Document,
            Warnings = new[] { "A warning" }
        };
    }
}
