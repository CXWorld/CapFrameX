using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    // The editor's layer list: every row spans the list, so the lock boxes form one column at its right
    // edge whatever the length of a name. Before, each row was as wide as its name and the box followed it.
    [STATestClass]
    [DoNotParallelize]
    public class OverlayTileListLayoutTest
    {
        [TestMethod]
        public void TheLockBoxesOfTheLayerListFormOneColumnAtItsRightEdge()
        {
            var host = new Border();
            TransitionAssist.SetDisableTransitions(host, true);
            host.Resources.MergedDictionaries.Add(new CustomColorTheme { BaseTheme = BaseTheme.Dark, PrimaryColor = Color.FromRgb(2, 113, 249), SecondaryColor = Colors.Lime });
            foreach (string resource in new[] { "MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml", "CapFrameX.View;component/Styles/CxTextFields.xaml" })
                host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + resource) });
            var document = new OsdDesignDocument { Width = 640, CanvasHeight = 900, IsCanvas = true, GridSize = 8 };
            string[] labels = { "REDLINE header", "1% LOW", "CORE / THREAD LOADS", "A very long layer name that does not fit the list" };
            for (int i = 0; i < labels.Length; i++)
                document.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = labels[i], Key = "fps", X = 0, Y = i * 96, FrameWidth = 240, FrameHeight = 88 });
            var editor = new OsdEditorControl();
            editor.SetTemplateJson(document.ToJson());
            host.Child = editor;
            var window = new Window { Content = host, Width = 1280, Height = 960, WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000 };
            window.Show();
            try
            {
                Settle();
                ((TabControl)editor.FindName("PaletteTabs")).SelectedIndex = 1;
                Settle(50);
                window.UpdateLayout();
                var list = (ListBox)editor.FindName("TileList");
                var boxes = Descendants<CheckBox>(list).ToArray();
                Assert.AreEqual(labels.Length, boxes.Length);
                var lefts = boxes.Select(box => box.TranslatePoint(new Point(0, 0), list).X).ToArray();
                foreach (double left in lefts)
                    Assert.AreEqual(lefts[0], left, .5, "Every lock box starts in the same column.");
                double right = lefts[0] + boxes[0].ActualWidth;
                Assert.IsTrue(list.ActualWidth - right < 40, $"The lock column sits at the right edge ({right:F0} of {list.ActualWidth:F0} px).");
                var longName = Descendants<TextBlock>(list).First(text => text.Text == labels[^1]);
                Assert.IsTrue(longName.TranslatePoint(new Point(longName.ActualWidth, 0), list).X <= lefts[0], "A long name ends before the lock column.");
            }
            finally
            {
                window.Close();
                editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                window.Content = null;
            }
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) yield return match;
                foreach (var descendant in Descendants<T>(child)) yield return descendant;
            }
        }

        private static void Settle(int milliseconds = 1)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }
}
