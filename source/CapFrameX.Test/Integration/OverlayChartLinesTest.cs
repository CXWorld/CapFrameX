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
    // Chart tiles with several lines: frametimes or display times with their adaptive stutter
    // threshold (OsdChartLine, the renderer's "lines").
    [STATestClass]
    public class OverlayChartLinesTest
    {
        private static readonly OsdTelemetrySource[] NoSources = Array.Empty<OsdTelemetrySource>();

        [TestMethod]
        public void ChartsWithoutLinesExportAsBefore()
        {
            var document = Grid(new OsdDesignTile { Kind = "chart", Key = "frametimes", Label = "Frametime", Unit = "ms", ColumnSpan = 2 });
            string json = document.ToJson();
            Assert.IsFalse(json.Contains("\"lines\""), "A chart without lines must not export a lines array.");
            Assert.IsFalse(json.Contains("chartLines"), "A chart without lines must not store chartLines.");
        }

        [TestMethod]
        public void ChartLinesRoundTripAndReachTheRenderer()
        {
            var document = Grid(OsdChartPresets.StutterChart("displaytimes"));
            string json = document.ToJson();

            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson(), "Chart lines must survive the editor round trip.");
            var widget = (JObject)JObject.Parse(json).SelectToken("$..children[?(@.type == 'chart')]");
            var lines = (JArray)widget["lines"];
            Assert.AreEqual("displaytimes", (string)widget["series"]);
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("value", (string)lines[0]["kind"]);
            Assert.AreEqual("stutter", (string)lines[1]["kind"]);
            Assert.AreEqual(OsdChartLine.DefaultStutterFactor, (double)lines[1]["factor"]);
            Assert.AreEqual(OsdChartLine.DefaultStutterColor, (string)lines[1]["color"]);
            Assert.IsFalse(string.IsNullOrEmpty((string)lines[1]["label"]), "The editor gives every line a legend label.");
        }

        [TestMethod]
        public void TheStutterChartsPresetIsValidOnCanvasAndGrid()
        {
            var canvas = new OsdDesignDocument { Width = 640, CanvasHeight = 480, IsCanvas = true };
            canvas.Tiles.Add(OsdChartPresets.CreateStutterCharts());
            canvas.Validate();
            Assert.AreEqual(OsdChartPresets.StutterChartsTileCount, canvas.EnumerateTiles().Count());

            var grid = new OsdDesignDocument { Width = 640, Columns = 2 };
            foreach (var chart in OsdChartPresets.CreateStutterChartTiles(2)) grid.Tiles.Add(chart);
            grid.Validate();
            CollectionAssert.AreEqual(new[] { "frametimes", "displaytimes" }, grid.Tiles.Select(tile => tile.Key).ToArray());
        }

        [TestMethod]
        [DataRow("framerates", "framerates:value,framerates:stutter", DisplayName = "Stutter threshold of FPS")]
        [DataRow("frametimes", "frametimes:value,framerates:value", DisplayName = "FPS beside milliseconds")]
        [DataRow("frametimes", "frametimes:value,frametimes:stutter,displaytimes:value,displaytimes:stutter,frametimes:value", DisplayName = "Five lines")]
        [DataRow("frametimes", "frametimes:stutter", DisplayName = "No value line")]
        [DataRow("displaytimes", "frametimes:value,frametimes:stutter", DisplayName = "Key is not the first value line")]
        [DataRow("frametimes", "frametimes:value,frametimes:average", DisplayName = "Unknown kind")]
        [DataRow("frametimes", "latency:value", DisplayName = "Unknown series")]
        public void InvalidChartLinesAreRejected(string key, string lines)
        {
            var chart = new OsdDesignTile
            {
                Kind = "chart", Key = key, Label = "Chart", ColumnSpan = 2,
                ChartLines = lines.Split(',').Select(line => line.Split(':'))
                    .Select(parts => new OsdChartLine { Series = parts[0], Kind = parts[1] }).ToList()
            };
            Assert.ThrowsExactly<FormatException>(() => Grid(chart).Validate());
        }

        [TestMethod]
        [DataRow(0.5)]
        [DataRow(10.5)]
        public void StutterFactorsOutsideTheirRangeAreRejected(double factor)
        {
            var chart = OsdChartPresets.StutterChart("frametimes");
            chart.ColumnSpan = 2;
            chart.ChartLines[1].Factor = factor;
            Assert.ThrowsExactly<FormatException>(() => Grid(chart).Validate());
        }

        [TestMethod]
        public void TheTileDocumentCarriesChartLinesAndChecksTheirFields()
        {
            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""width"": 320,
                ""tiles"": [{ ""kind"": ""chart"", ""key"": ""frametimes"", ""label"": ""Frametime"", ""frameHeight"": 120,
                    ""chartLines"": [{ ""series"": ""frametimes"" }, { ""series"": ""frametimes"", ""kind"": ""stutter"", ""factor"": 3 }] }]
                }"), "Lines", NoSources);
            var lines = document.Tiles[0].ChartLines;
            Assert.AreEqual(2, lines.Count);
            Assert.IsTrue(lines[1].IsStutter);
            Assert.AreEqual(3, lines[1].Factor);
            var exchanged = OverlayTileDocument.FromJson(OverlayTileDocument.ToJson(document), document.Name, NoSources);
            Assert.AreEqual(document.ToJson(), exchanged.ToJson(), "Chart lines must survive the tile document.");

            var error = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""tiles"": [{ ""kind"": ""chart"", ""key"": ""frametimes"", ""chartLines"": [{ ""series"": ""frametimes"", ""factr"": 3 }] }]
                }"), "Typo", NoSources));
            StringAssert.Contains(error.Message, "chart line 0");
        }

        [TestMethod]
        public void TheRuntimeFeedsEverySeriesTheLinesDraw()
        {
            var document = Grid(new OsdDesignTile
            {
                Kind = "chart", Key = "frametimes", Label = "Frames", ColumnSpan = 2,
                ChartLines = new List<OsdChartLine>
                {
                    new OsdChartLine { Series = "frametimes" },
                    new OsdChartLine { Series = "displaytimes", Kind = OsdChartLine.StutterKind }
                }
            });
            var design = OverlayRuntimeDesign.Parse("id", "Lines", document.ToJson());
            Assert.IsTrue(design.NeedsFrametimes);
            Assert.IsTrue(design.NeedsDisplayTimes, "A display time threshold needs the display time feed.");
        }

        private static OsdDesignDocument Grid(OsdDesignTile chart)
        {
            var document = new OsdDesignDocument { Width = 640, Columns = 2 };
            document.Tiles.Add(chart);
            return document;
        }
    }
}
