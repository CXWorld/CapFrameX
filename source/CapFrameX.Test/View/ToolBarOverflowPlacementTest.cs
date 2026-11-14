using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CapFrameX.View.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.View
{
    /// <summary>
    /// The analysis tool bar sits at the bottom of the window, so its overflow menu opens upwards,
    /// right-aligned with the overflow button, instead of over the status bar and the taskbar.
    /// </summary>
    [STATestClass]
    public class ToolBarOverflowPlacementTest
    {
        [TestMethod]
        public void FindOverflowPopup_FindsThePopupThatHostsTheOverflowPanel()
        {
            var popup = ToolBarOverflowPlacement.FindOverflowPopup(NewToolBar());

            Assert.IsNotNull(popup);
        }

        [TestMethod]
        public void OpensUpward_PlacesTheMenuByCallbackOnceTheToolBarIsLoaded()
        {
            var toolBar = NewToolBar();
            ToolBarOverflowPlacement.SetOpensUpward(toolBar, true);

            toolBar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            var popup = ToolBarOverflowPlacement.FindOverflowPopup(toolBar);
            Assert.AreEqual(PlacementMode.Custom, popup.Placement);
            Assert.IsNotNull(popup.CustomPopupPlacementCallback);
        }

        [TestMethod]
        public void OpensUpward_Off_LeavesTheTemplatePlacement()
        {
            var toolBar = NewToolBar();
            ToolBarOverflowPlacement.SetOpensUpward(toolBar, true);
            ToolBarOverflowPlacement.SetOpensUpward(toolBar, false);

            toolBar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.AreNotEqual(PlacementMode.Custom, ToolBarOverflowPlacement.FindOverflowPopup(toolBar).Placement);
        }

        [TestMethod]
        public void PlaceAboveRightAligned_PrefersAboveAndFallsBackBelow()
        {
            var placements = ToolBarOverflowPlacement.PlaceAboveRightAligned(new Size(200, 80), new Size(30, 30), new Point());

            Assert.AreEqual(2, placements.Length);
            Assert.AreEqual(new Point(-170, -80), placements[0].Point, "Above, right edges aligned.");
            Assert.AreEqual(new Point(-170, 30), placements[1].Point, "Below as the fallback.");
        }

        // A tool bar created in code gets its theme style, and so its template, once initialized.
        private static ToolBar NewToolBar()
        {
            var toolBar = new ToolBar();
            toolBar.BeginInit();
            toolBar.EndInit();
            return toolBar;
        }
    }
}
