using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// Which entries OverlayService sends to RTSS. The native renderer adds a row only for
    /// ShowOnOverlay and a graph only for ShowGraph (RTSSCoreControl::AddOverlayEntry and the
    /// graph section of Refresh), so no other entry can change what RTSS shows.
    /// </summary>
    [TestClass]
    public class RtssEntrySelectionTest
    {
        [TestMethod]
        public void HiddenRows_AreNotSent()
        {
            var selected = OverlayService.SelectRtssEntries(new[]
            {
                Entry("CpuLoad", showOnOverlay: true),
                Entry("CpuTemp", showOnOverlay: false),
                Entry("GpuLoad", showOnOverlay: true),
            });

            CollectionAssert.AreEqual(new[] { "CpuLoad", "GpuLoad" }, Identifiers(selected));
        }

        [TestMethod]
        public void HiddenRowWithItsGraphOn_IsSent()
        {
            // RTSS draws the framerate and frametime graphs even when their rows are hidden.
            var selected = OverlayService.SelectRtssEntries(new[]
            {
                Entry("Framerate", showOnOverlay: false, showGraph: true),
                Entry("Frametime", showOnOverlay: false, showGraph: false),
            });

            CollectionAssert.AreEqual(new[] { "Framerate" }, Identifiers(selected));
        }

        [TestMethod]
        public void DisabledEntries_AreNotSentEvenWhenVisible()
        {
            var selected = OverlayService.SelectRtssEntries(new[]
            {
                Entry("CpuLoad", showOnOverlay: true, enabled: false),
                Entry("Framerate", showOnOverlay: false, showGraph: true, enabled: false),
                Entry("Frametime", showOnOverlay: true, showGraph: true, enabled: false),
            });

            Assert.AreEqual(0, selected.Length);
        }

        [TestMethod]
        public void SentEntries_KeepTheirOrder()
        {
            var selected = OverlayService.SelectRtssEntries(new[]
            {
                Entry("RunHistory", showOnOverlay: true),
                Entry("Hidden1", showOnOverlay: false),
                Entry("Framerate", showOnOverlay: true, showGraph: true),
                Entry("Hidden2", showOnOverlay: false),
                Entry("CaptureServiceStatus", showOnOverlay: true),
            });

            CollectionAssert.AreEqual(new[] { "RunHistory", "Framerate", "CaptureServiceStatus" },
                Identifiers(selected));
        }

        [TestMethod]
        public void NothingVisible_SendsAnEmptyList()
        {
            // The wrapper clears its rows on an empty list, so a profile with everything
            // switched off leaves no stale rows behind.
            var selected = OverlayService.SelectRtssEntries(new[]
            {
                Entry("CpuLoad", showOnOverlay: false),
                Entry("GpuLoad", showOnOverlay: false),
            });

            Assert.AreEqual(0, selected.Length);
        }

        private static IOverlayEntry Entry(string identifier, bool showOnOverlay,
            bool showGraph = false, bool enabled = true)
            => new OverlayEntryWrapper(identifier)
            {
                ShowOnOverlay = showOnOverlay,
                ShowGraph = showGraph,
                IsEntryEnabled = enabled
            };

        private static string[] Identifiers(IOverlayEntry[] entries)
            => entries.Select(entry => entry.Identifier).ToArray();
    }
}
