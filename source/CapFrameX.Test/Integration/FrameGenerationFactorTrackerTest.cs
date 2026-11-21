using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// The frame generation factor is measured, not read from a setting: displayed frames over
    /// the frames the game rendered, from the counters the in-game hook publishes. The rates
    /// below are the ones a real DLSS, FSR and XeSS frame generation produced on an RTX 5090.
    /// </summary>
    [TestClass]
    public class FrameGenerationFactorTrackerTest
    {
        private const int Unknown = 0;
        private const int Dlss = 1;
        private const int Xess = 2;
        private const int Fsr = 3;
        private const int ActivityUnknown = 0;
        private const int Active = 2;

        [TestMethod]
        public void Dlss_MeasuresTwoThreeAndFourTimesFromTheFrameTokens()
        {
            // The generic route hooks no Streamline swapchain: no proxy Present is counted.
            foreach (ulong multiplier in new ulong[] { 2, 3, 4 })
            {
                var hook = new Hook();
                var tracker = new FrameGenerationFactorTracker();
                Assert.IsNull(Observe(tracker, hook, Dlss), "One publication is no span.");
                hook.Advance(1000, displayed: 60 * multiplier, streamline: 60);
                Assert.IsNull(Observe(tracker, hook, Dlss),
                    "Nothing shows yet whether the counter ran before this span.");
                hook.Advance(1000, displayed: 60 * multiplier, streamline: 60);

                Assert.AreEqual((double)multiplier, Observe(tracker, hook, Dlss).Value, 1e-9);
            }
        }

        [TestMethod]
        public void Fsr_MeasuresFromThePrepareDispatch_Xess_FromTheFrameConstants()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Fsr);
                // A late attach misses the FidelityFX swapchain: 207 displayed, 103 prepared.
                hook.Advance(1000, displayed: 104, fsrPrepared: 52, fsrGenerated: 52);
            }
            Assert.AreEqual(2.0, Observe(tracker, hook, Fsr).Value, 1e-9);

            hook = new Hook();
            tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Xess);
                hook.Advance(1000, displayed: 93, xess: 31);
            }
            Assert.AreEqual(3.0, Observe(tracker, hook, Xess).Value, 1e-9);
        }

        [TestMethod]
        public void EveryTechnologyCountsItsOwnFrames()
        {
            // A game that uses Streamline for latency markers and FSR for frame generation asks
            // for frame tokens too; an engine may ask for two per frame.
            var hook = new Hook();
            var fsr = new FrameGenerationFactorTracker();
            var dlss = new FrameGenerationFactorTracker();
            var none = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(fsr, hook, Fsr);
                Observe(dlss, hook, Dlss);
                Observe(none, hook, Unknown);
                hook.Advance(1000, displayed: 120, streamline: 120, fsrPrepared: 60);
            }

            Assert.AreEqual(2.0, Observe(fsr, hook, Fsr).Value, 1e-9);
            Assert.AreEqual(1.0, Observe(dlss, hook, Dlss).Value, 1e-9);
            Assert.IsNull(Observe(none, hook, Unknown), "Without a technology no call counts frames.");
        }

        [TestMethod]
        public void TheApplicationsOwnPresentCountsBeforeTheCallsToTheRuntime()
        {
            // Where the hook sees the Present to the runtime's swapchain, that is the frame. Two
            // frame tokens per frame would halve the factor.
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 240, proxy: 60, streamline: 120);
            }

            Assert.AreEqual(4.0, Observe(tracker, hook, Dlss).Value, 1e-9);
        }

        [TestMethod]
        public void ACounterThatStartsInsideTheSpanDoesNotMeasureIt()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 3; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 120, streamline: 60);
            }
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);

            // The proxy hooks arm in the middle of a second: 20 of its 60 Presents are counted.
            // 120 displayed frames over 20 would read as six times.
            hook.Advance(1000, displayed: 120, proxy: 20, streamline: 60);
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);
            hook.Advance(1000, displayed: 120, proxy: 60, streamline: 60);
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9,
                "The span still begins before the proxy counted.");
            hook.Advance(1000, displayed: 120, proxy: 60, streamline: 60);
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);
        }

        [TestMethod]
        public void ACounterThatStoppedNamesNoFactor()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 3; i++)
            {
                Observe(tracker, hook, Fsr);
                hook.Advance(1000, displayed: 120, fsrPrepared: 60);
            }
            Assert.AreEqual(2.0, Observe(tracker, hook, Fsr).Value, 1e-9);

            // The frame generation context is gone; the game presents on.
            hook.Advance(1000, displayed: 60);
            Assert.IsNull(Observe(tracker, hook, Fsr));
        }

        [TestMethod]
        public void FrameGenerationThatGeneratesNothingIsOneTimes()
        {
            // Switched on, but every displayed frame is a rendered one (Control Resonant while
            // the cursor is visible, DLSS frame generation in a hidden window).
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 60, proxy: 60, streamline: 60);
            }

            Assert.AreEqual(1.0, Observe(tracker, hook, Dlss).Value, 1e-9);
        }

        [TestMethod]
        public void TheSpanIsAboutTwoSecondsAndFollowsASwitch()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 6; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(500, displayed: 60, streamline: 30);
            }
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);

            // The game switches to four times.
            for (int i = 0; i < 3; i++)
            {
                hook.Advance(500, displayed: 120, streamline: 30);
                double mixed = Observe(tracker, hook, Dlss).Value;
                Assert.IsTrue(mixed > 2.0 && mixed < 4.0, $"{mixed} lies between both factors.");
            }
            hook.Advance(500, displayed: 120, streamline: 30);
            Assert.AreEqual(4.0, Observe(tracker, hook, Dlss).Value, 1e-9,
                "Two seconds after the switch only the new factor is left.");
        }

        [TestMethod]
        public void ASlowOverlayRefreshStillMeasures()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 2; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(5000, displayed: 900, streamline: 300);
            }
            Assert.AreEqual(3.0, Observe(tracker, hook, Dlss).Value, 1e-9);

            // The same publication read twice (a row and a tile ask in one refresh) is one.
            Assert.AreEqual(3.0, Observe(tracker, hook, Dlss).Value, 1e-9);
        }

        [TestMethod]
        public void TooFewFramesOrTooShortASpanNameNoFactor()
        {
            // Five rendered frames per second: one second says too little, two do.
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 2; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 20, streamline: 5);
            }
            Assert.IsNull(Observe(tracker, hook, Dlss));
            hook.Advance(1000, displayed: 20, streamline: 5);
            Assert.AreEqual(4.0, Observe(tracker, hook, Dlss).Value, 1e-9);

            // Publications a quarter of a second apart: no span of 900 ms yet.
            hook = new Hook();
            tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(250, displayed: 60, streamline: 30);
            }
            Assert.IsNull(Observe(tracker, hook, Dlss), "750 ms between the second and the last one.");
            hook.Advance(250, displayed: 60, streamline: 30);
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);
        }

        [TestMethod]
        public void AGameThatPresentsNothingNamesNoFactor()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 3; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 120, streamline: 60);
            }
            HookFrameCounts last = hook.Publish();

            Assert.AreEqual(2.0, tracker.Observe(last, hook.Tick + 1500, Dlss, Active).Value, 1e-9);
            Assert.IsNull(tracker.Observe(last, hook.Tick + 1501, Dlss, Active),
                "The hook publishes with every displayed frame; none came for 1.5 s.");
            Assert.AreEqual(2.0, tracker.Observe(last, hook.Tick - 5, Dlss, Active).Value, 1e-9,
                "The hook stamped between the two clock reads.");

            // A pause adds to neither side: the span across it still measures.
            hook.Advance(6000, displayed: 120, streamline: 60);
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);
        }

        [TestMethod]
        public void CountersThatWentBackStartTheMeasurementOver()
        {
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 3; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 120, streamline: 60);
            }
            Assert.AreEqual(2.0, Observe(tracker, hook, Dlss).Value, 1e-9);

            // The hook was removed and attached again: its counters begin at zero.
            var attachedAgain = new Hook { Tick = hook.Tick + 1000 };
            attachedAgain.Advance(0, displayed: 30, streamline: 10);
            Assert.IsNull(Observe(tracker, attachedAgain, Dlss));
            attachedAgain.Advance(1000, displayed: 180, streamline: 60);
            Assert.IsNull(Observe(tracker, attachedAgain, Dlss));
            attachedAgain.Advance(1000, displayed: 180, streamline: 60);
            Assert.AreEqual(3.0, Observe(tracker, attachedAgain, Dlss).Value, 1e-9);

            tracker.Reset();
            Assert.IsNull(Observe(tracker, attachedAgain, Dlss), "A reset tracker has one publication.");
        }

        [TestMethod]
        public void CountsThatDescribeNoFrameGenerationNameNoFactor()
        {
            // Two frame tokens per frame and no Present of the application to tell: half a
            // displayed frame per "frame" is no factor.
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 3; i++)
            {
                Observe(tracker, hook, Dlss);
                hook.Advance(1000, displayed: 60, streamline: 120);
            }
            Assert.IsNull(Observe(tracker, hook, Dlss));

            // A call that comes once in ten frames.
            hook = new Hook();
            tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 3; i++)
            {
                Observe(tracker, hook, Xess);
                hook.Advance(1000, displayed: 200, xess: 20);
            }
            Assert.IsNull(Observe(tracker, hook, Xess));
        }

        [TestMethod]
        public void WithoutTelemetry_AMeasurementThatShowsGeneratedFramesNamesTheFactor()
        {
            // XeSS-FG switched on before the hook attached: the game never calls the switch
            // again, so the telemetry knows neither the technology nor the state. The counters
            // do: 186 displayed frames per 93 tagged ones.
            var hook = new Hook();
            var tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Unknown, ActivityUnknown);
                hook.Advance(1000, displayed: 93, xess: 46);
            }
            Assert.AreEqual(93.0 / 46.0, Observe(tracker, hook, Unknown, ActivityUnknown).Value, 1e-9);

            // The same with the application's Present to the runtime, and for FSR.
            hook = new Hook();
            tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Unknown, ActivityUnknown);
                hook.Advance(1000, displayed: 180, proxy: 60);
            }
            Assert.AreEqual(3.0, Observe(tracker, hook, Unknown, ActivityUnknown).Value, 1e-9);

            hook = new Hook();
            tracker = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(tracker, hook, Unknown, ActivityUnknown);
                hook.Advance(1000, displayed: 120, fsrPrepared: 60);
            }
            Assert.AreEqual(2.0, Observe(tracker, hook, Unknown, ActivityUnknown).Value, 1e-9);
        }

        [TestMethod]
        public void WithoutTelemetry_NothingIsNamedThatTheMeasurementDoesNotProve()
        {
            // A resident runtime that generates nothing: "1x" needs the telemetry to say that
            // frame generation is on at all.
            var hook = new Hook();
            var unknown = new FrameGenerationFactorTracker();
            var active = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(unknown, hook, Unknown, ActivityUnknown);
                Observe(active, hook, Xess);
                hook.Advance(1000, displayed: 60, proxy: 60, xess: 60);
            }
            Assert.IsNull(Observe(unknown, hook, Unknown, ActivityUnknown));
            Assert.AreEqual(1.0, Observe(active, hook, Xess).Value, 1e-9);

            // Streamline frame tokens alone prove nothing: a game asks for them without any
            // frame generation, and a loading screen presents without asking.
            hook = new Hook();
            unknown = new FrameGenerationFactorTracker();
            active = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(unknown, hook, Dlss, ActivityUnknown);
                Observe(active, hook, Dlss);
                hook.Advance(1000, displayed: 180, streamline: 60);
            }
            Assert.IsNull(Observe(unknown, hook, Dlss, ActivityUnknown));
            Assert.AreEqual(3.0, Observe(active, hook, Dlss).Value, 1e-9);

            // Two runtimes called at once do not say which one presents.
            hook = new Hook();
            unknown = new FrameGenerationFactorTracker();
            for (int i = 0; i < 4; i++)
            {
                Observe(unknown, hook, Unknown, ActivityUnknown);
                hook.Advance(1000, displayed: 120, fsrPrepared: 60, xess: 60);
            }
            Assert.IsNull(Observe(unknown, hook, Unknown, ActivityUnknown));
        }

        private static double? Observe(FrameGenerationFactorTracker tracker, Hook hook, int technology,
            int activity = Active)
            => tracker.Observe(hook.Publish(), hook.Tick, technology, activity);

        // The counters of a hook, published at the moment they are read.
        private sealed class Hook
        {
            private ulong _displayed, _proxy, _streamline, _fsrPrepared, _fsrGenerated, _xess;

            public ulong Tick { get; set; } = 1_000_000;

            public void Advance(ulong milliseconds, ulong displayed = 0, ulong proxy = 0,
                ulong streamline = 0, ulong fsrPrepared = 0, ulong fsrGenerated = 0, ulong xess = 0)
            {
                Tick += milliseconds;
                _displayed += displayed;
                _proxy += proxy;
                _streamline += streamline;
                _fsrPrepared += fsrPrepared;
                _fsrGenerated += fsrGenerated;
                _xess += xess;
            }

            public HookFrameCounts Publish()
                => new HookFrameCounts(Tick, _displayed, _proxy, _streamline, _fsrPrepared,
                    _fsrGenerated, _xess);
        }
    }
}
