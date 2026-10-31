using System;
using System.Linq;
using System.Runtime.InteropServices;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The hybrid starters combine tiles with the linked row profile, whose length only the user knows: their
    /// row block follows its rows and comes last, so the whole profile shows and no row is cut.
    /// </summary>
    [TestClass]
    public class OverlayHybridStarterTest
    {
        [TestMethod]
        [DataRow("hybrid-benchmark")]
        [DataRow("hybrid-compact")]
        [DataRow("hybrid-thread-deck")]
        public void HybridStarterEndsWithARowBlockThatFollowsItsRows(string presetId)
        {
            var starter = Starter(presetId);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            Assert.AreEqual(starter.DesignJson, OverlayRuntimeDesign.Canonicalize(document.ToJson()), "The starter is stored as the editor saves it.");
            CollectionAssert.AreEqual(OsdDesignPresets.Find(presetId).CreateDocument().Tiles.Select(tile => tile.Kind).ToArray(),
                document.Tiles.Select(tile => tile.Kind).ToArray(), "The starter follows its gallery template.");

            var rows = document.Tiles.Last();
            Assert.IsTrue(rows.FollowsRows);
            Assert.AreEqual(1, document.Tiles.Count(tile => tile.Kind == "classicRows"));
            foreach (var tile in document.Tiles.Take(document.Tiles.Count - 1))
                Assert.IsTrue(document.TileFrame(tile).Bottom <= document.TileFrame(rows).Top, tile.Label + " must sit above the rows, which grow downward.");
            var panel = JObject.Parse(starter.DesignJson)["root"]["children"].Last;
            Assert.IsNull(panel["frameHeight"], "The canvas sizes the row block to its rows.");
            Assert.AreEqual(0, (double)panel["children"][0]["height"], "The renderer measures the rows.");
        }

        [TestMethod]
        [DataRow("hybrid-benchmark")]
        [DataRow("hybrid-compact")]
        [DataRow("hybrid-thread-deck")]
        public void UnmodifiedVersionOneCopyGetsTheRowsThatFollowTheirProfile(string presetId)
        {
            var starter = Starter(presetId);
            Assert.AreEqual(1, starter.IntroducedInVersion);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            // Version one: rows of a fixed height right below the headlines, above the frametime strip or the
            // thread loads, which Hybrid Thread Deck drew in magenta on a dark red panel.
            var rows = document.Tiles.Last();
            rows.ClassicRowsAutoHeight = null;
            if (document.Tiles.Count == 4)
            {
                var below = document.Tiles[2];
                rows.Y = below.Y;
                below.Y = Math.Round(rows.Y.Value + rows.FrameHeight.Value + document.Gap, 3);
                document.Tiles.Move(3, 2);
                if (presetId == "hybrid-thread-deck")
                {
                    below.Color = "#FF4297FF";
                    below.BackgroundColor = "#1C0D1BED";
                }
            }
            Assert.IsTrue(starter.CanUpdate(new OverlayDesignProfile(starter.Id, starter.Name, document.ToJson(), DateTime.UtcNow)),
                "An unmodified version-one copy gets the rows that follow their profile.");
            Assert.IsFalse(starter.CanUpdate(new OverlayDesignProfile(starter.Id, starter.Name, starter.DesignJson, DateTime.UtcNow)),
                "The current design is never replaced.");
        }

        [TestMethod]
        public void HybridThreadDeckDrawsItsThreadLoadsInTheDesignAccentOnItsNeutralPanel()
        {
            var starter = Starter("hybrid-thread-deck");
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            var threads = document.Tiles.Single(tile => tile.Kind == "group");
            Assert.AreEqual(document.Accent, threads.Color);
            Assert.IsNull(threads.BackgroundColor, "The thread loads take the design's tile color like every other tile.");

            var scene = JObject.Parse(starter.DesignJson);
            foreach (var panel in scene["root"]["children"])
                Assert.AreEqual((string)scene["theme"]["panel"], (string)panel["color"]);
            var group = scene["root"]["children"].Single(panel => (string)panel["children"][0]["editor"]["kind"] == "group")["children"][0];
            Assert.AreEqual(document.Accent, (string)group["children"][0]["color"], "The title takes the accent.");
            foreach (var bar in group["children"][1]["children"])
                Assert.AreEqual(document.Accent, (string)bar["color"], "The bars take the accent.");
        }

        [TestMethod]
        public void BindingTheThreadLoadsMovesTheRowsDownWithThem()
        {
            var starter = Starter("hybrid-thread-deck");
            // 16 threads wrap onto two rows of bars, so the group grows over the rows' former top.
            var catalog = Enumerable.Range(1, 16).Select(index => new OverlayTelemetrySource
            {
                Id = "cpu0/cpu-loads/" + index, DeviceKey = "cpu0", HardwareName = "cpu0", Name = "CPU Core #" + index,
                GroupKey = "cpu0/cpu-loads", Category = "CPU", Unit = "%", GroupOrder = index, IsAvailable = true
            }).ToArray();
            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson), catalog);
            var tiles = (JArray)JObject.Parse(bound.TemplateJson)["root"]["children"];
            var threads = tiles[2];
            var rows = tiles[3];
            Assert.AreEqual(16, threads["children"][0]["children"][1]["children"].Count());
            Assert.IsTrue((double)threads["frameHeight"] > (double)JObject.Parse(starter.DesignJson)["root"]["children"][2]["frameHeight"],
                "The bound thread loads need more room than the template.");
            Assert.AreEqual((double)threads["y"] + (double)threads["frameHeight"] + 8, (double)rows["y"], .001, "The rows stay below the thread loads.");
            Assert.IsNull(rows["frameHeight"], "The rows still take the height of their profile.");
        }

        [TestMethod]
        [DataRow("hybrid-benchmark")]
        [DataRow("hybrid-compact")]
        [DataRow("hybrid-thread-deck")]
        public void RowBlockTakesTheHeightOfTheRowProfileWithoutMovingTheOtherTiles(string presetId)
        {
            var starter = Starter(presetId);
            int tileCount = OsdDesignDocument.FromJson(starter.DesignJson).Tiles.Count;
            const int width = 1600, height = 2400, stride = width * 4;
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(starter.DesignJson), "Native renderer rejected the starter.");
            Assert.IsTrue(preview.SetMetricsJson("{\"fps\":144,\"frametime\":6.9,\"fps1pctLow\":118}"));
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                // Tile frames and the design's size in design pixels; the preview fits the design into its bitmap.
                (double X, double Y, double Width, double Height)[] Layout(int rows)
                {
                    preview.UpdateEntries(Entries(rows));
                    for (int frame = 0; frame < 4; frame++)
                        Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, .05));
                    var bounds = new OsdPreviewTileRect[8];
                    Assert.AreEqual(tileCount, preview.GetLayout(out var layout, bounds));
                    double scale = layout.Scale;
                    return bounds.Take(tileCount).Select(b => ((double)(b.X - layout.X) / scale, (double)(b.Y - layout.Y) / scale,
                            (double)b.Width / scale, (double)b.Height / scale))
                        .Append((0.0, 0.0, layout.Width / scale, layout.Height / scale)).ToArray();
                }
                var few = Layout(2);
                var many = Layout(30);
                var fewRows = few[tileCount - 1];
                var manyRows = many[tileCount - 1];
                Assert.IsTrue(fewRows.Height < 120, "Two rows take little room: " + fewRows.Height);
                Assert.IsTrue(manyRows.Height > fewRows.Height + 28 * 16, "Thirty rows and a graph are measured, not clipped: " + manyRows.Height);
                Assert.AreEqual(fewRows.Y, manyRows.Y, .01, "The rows start where they did.");
                for (int index = 0; index < tileCount - 1; index++)
                {
                    Assert.AreEqual(few[index].X, many[index].X, .01);
                    Assert.AreEqual(few[index].Y, many[index].Y, .01);
                    Assert.AreEqual(few[index].Height, many[index].Height, .01, "A tile above the rows changed with them.");
                }
                Assert.AreEqual(manyRows.Y + manyRows.Height, many[tileCount].Height, 1, "The design grows with its rows.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static OverlayDesignStarterProfile Starter(string presetId)
            => OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);

        // A row per entry, the last one with its frametime graph.
        private static OsdEntry[] Entries(int count) => Enumerable.Range(0, count).Select(index =>
        {
            bool graph = count > 2 && index == count - 1;
            return new OsdEntry
            {
                Identifier = graph ? "Frametime" : "row-" + index, Group = "Row " + index, Label = graph ? "Frametime" : "Reading " + index,
                Value = graph ? 6.9 : 40 + index, IsNumeric = true, Unit = graph ? "ms" : "%", Digits = graph ? 1 : 0,
                Color = 0xF3F6FAFF, ShowGraph = graph
            };
        }).ToArray();
    }
}
