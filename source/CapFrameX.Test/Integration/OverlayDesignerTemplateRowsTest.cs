using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignerTemplateRowsTest
    {
        [DataTestMethod]
        [DataRow(8)]
        [DataRow(16)]
        [DataRow(24)]
        [DataRow(32)]
        public void OpeningThermalTemplateKeepsTwoBalancedRowsInPreviewAndInspector(int count)
        {
            var editor = new OsdEditorControl
            {
                Sources = Enumerable.Range(1, count).Select(index => new OsdTelemetrySource
                {
                    Key = "cpu/effective/" + index,
                    Name = $"CPU Core #{(index + 1) / 2} Thread #{(index - 1) % 2 + 1} Clock (Effective)",
                    DeviceKey = "cpu", DeviceName = "CPU", Category = "CPU",
                    GroupKey = "cpu-effective-clocks", GroupName = "CPU effective clocks",
                    GroupOrder = index, Unit = "MHz", IsAvailable = true
                }).ToArray()
            };
            var window = new Window
            {
                Content = editor, Width = 1280, Height = 960, ShowActivated = false,
                ShowInTaskbar = false, Left = -10000, Top = -10000
            };
            try
            {
                window.Show();
                typeof(OsdEditorControl).GetMethod("UsePreset", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, new object[] { OsdDesignPresets.Find("vertical-thermal-panel") });
                var tile = editor.Document.Tiles.Single(item => item.Label.StartsWith("EFFECTIVE CLOCKS", StringComparison.Ordinal));
                Assert.IsTrue(editor.SelectElement(tile));
                Settle();
                window.UpdateLayout();

                Assert.IsTrue(editor.TryCommitEdits());
                var layout = editor.Document.GetGroupBarLayout(tile);
                Assert.AreEqual(2, layout.Rows);
                Assert.AreEqual(count / 2, layout.Columns);
                Assert.AreEqual(2, ((ComboBox)editor.FindName("GroupBarRows")).SelectedItem,
                    "Realizing the inspector must not select another row count.");
                Assert.AreEqual(layout.EffectiveWidth,
                    double.Parse(((TextBox)editor.FindName("GroupBarWidthField")).Text, CultureInfo.CurrentCulture), .001);
                string saved = editor.GetTemplateJson();
                var flow = OsdDesignPresets.Find("vertical-thermal-panel").CreateDocument();
                flow.BindPresetSources(editor.Sources);
                using var native = new OsdPreview();
                Assert.IsTrue(native.CreateBitmap(800, 2000));
                Assert.IsTrue(native.LoadTemplateJson(flow.ToJson()));
                var bounds = new OsdPreviewTileRect[OsdDesignDocument.MaximumTiles];
                Assert.AreEqual(flow.Tiles.Count, native.GetLayout(out var nativeLayout, bounds));
                int tileIndex = editor.Document.Tiles.IndexOf(tile);
                // Canvas conversion uses DirectWrite's measured heading, which is shorter
                // than the model's conservative font estimate. Compare the actual bounds.
                Assert.AreEqual(bounds[tileIndex].Height / (nativeLayout.Scale * flow.Scale),
                    tile.FrameHeight.Value, .002);
                var grid = JsonNode.Parse(saved)["root"]["children"][tileIndex]["children"][0]["children"][1];
                Assert.AreEqual(layout.GridHeight, grid["height"].GetValue<double>(), .001);
                foreach (var member in grid["children"].AsArray())
                {
                    Assert.IsTrue(member["y"].GetValue<double>() + member["frameHeight"].GetValue<double>() <=
                        grid["height"].GetValue<double>() + .001, "Both bar and caption rows must fit the native group canvas.");
                }
                editor.SetTemplateJson(saved);
                Settle();
                Assert.AreEqual(saved, editor.GetTemplateJson(), "Reopening must retain the balanced rows.");
            }
            finally
            {
                window.Close();
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                window.Content = null;
            }
        }

        private static void Settle()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(350) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }
}
