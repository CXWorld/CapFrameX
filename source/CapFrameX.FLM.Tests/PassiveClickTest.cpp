#include "../CapFrameX.FLM/FlmPassiveClickTracker.h"
#include "../CapFrameX.FLM/FlmSadFilter.h"
#include <cmath>
#include <iostream>
#include <stdexcept>

using Tracker = FlmPassiveClickTracker;

static int assertions = 0;
static void Check(bool value, const char* message)
{
    ++assertions;
    if (!value)
        throw std::runtime_error(message);
}

static void Ready(Tracker& tracker, bool held = false)
{
    tracker.Reset(1000000, held, 2);
    int64_t input = 0;
    tracker.ObserveFrame(1000000, 1000000, false, input);
    tracker.ObserveFrame(1010000, 1010000, false, input);
}

int main()
{
    try
    {
        Tracker tracker;
        int64_t input = 0;
        Ready(tracker);
        tracker.ObserveButton(true, 1011000, true);
        Check(tracker.ObserveFrame(1020000, 1022000, true, input), "First valid click must be published on its response frame");
        Check(input == 1011000, "Preserve the original click timestamp");
        // The scene never returns to idle after this response. Publication must already be complete.
        for (int i = 1; i <= 10; ++i)
            Check(!tracker.ObserveFrame(1020000 + i * 10000, 1022000 + i * 10000, true, input), "Only one response per click");
        tracker.ObserveButton(true, 1130000, true);
        Check(tracker.GetSnapshot(1130000).clicks == 1, "Holding the button is not another click");

        Ready(tracker, true);
        tracker.ObserveButton(true, 1011000, true);
        Check(!tracker.HasPendingClick(), "A button held at startup is not a fresh click");
        tracker.ObserveButton(false, 1012000, true);
        tracker.ObserveButton(true, 1013000, true);
        Check(tracker.ObserveFrame(1020000, 1021000, true, input), "Release and press arms a new measurement");

        Ready(tracker);
        tracker.ObserveButton(true, 1011000, true);
        Check(!tracker.ObserveFrame(1010500, 1015000, true, input), "A buffered pre-click frame cannot be a response");
        Check(tracker.HasPendingClick(), "Keep waiting after a pre-click frame");
        Check(tracker.ObserveFrame(1020000, 1021000, true, input), "Use the first post-click response frame");

        Ready(tracker);
        tracker.ObserveButton(true, 1011000, true);
        tracker.ObserveFrame(1300000, 1300000, false, input);
        Check(tracker.GetSnapshot(1312000).state == Tracker::State::NoResponse, "Missing responses must time out visibly");
        Check(tracker.GetSnapshot(1313000).timeouts == 1, "Count a timeout once");
        Check(!tracker.ObserveFrame(1320000, 1320000, true, input), "A late response cannot revive an expired click");
        tracker.ObserveButton(true, 1330000, true);
        Check(!tracker.HasPendingClick(), "A held button must not re-arm after timeout");

        Ready(tracker);
        tracker.ObserveFrame(1020000, 1020000, true, input);
        tracker.ObserveButton(true, 1021000, true);
        tracker.ObserveFrame(1030000, 1030000, false, input);
        tracker.ObserveButton(true, 1031000, true);
        Check(!tracker.HasPendingClick(), "A rejected moving-scene click must not be timestamped later");
        Check(tracker.GetSnapshot(1031000).rejectedClicks == 1, "Count a rejected click");

        Ready(tracker);
        tracker.ObserveButton(true, 1400000, true);
        Check(!tracker.HasPendingClick(), "Do not arm against stale capture");
        Check(tracker.GetSnapshot(1400000).state == Tracker::State::NoFrames, "Expose stalled capture");

        Ready(tracker);
        tracker.ObserveButton(true, 1011000, true);
        tracker.ObserveButton(false, 1012000, true);
        tracker.ObserveButton(true, 1013000, true);
        Check(!tracker.ObserveFrame(1020000, 1020000, true, input), "Reject ambiguous overlapping clicks");
        Check(tracker.GetSnapshot(1020000).rejectedClicks == 1, "Record ambiguous click rejection");

        Ready(tracker);
        tracker.ObserveButton(true, 1011000, true);
        tracker.ObserveButton(true, 1012000, false);
        Check(!tracker.HasPendingClick(), "Stopping cancels the pending click");
        Check(!tracker.ObserveFrame(1030000, 1020000, true, input), "Reject future frame timestamps");

        // Run the production baseline estimator against steady motion and a small response.
        FlmSadFilter strict, sensitive;
        const float alpha = std::exp(std::log(0.01f) / 100);
        for (int i = 0; i < 1000; ++i)
        {
            strict.Process(100, 5, alpha);
            sensitive.Process(100, 3, alpha);
        }
        Check(strict.Process(400, 5, alpha, false) == 0, "Reproduce the former default missing a small response");
        Check(sensitive.Process(400, 3, alpha, false) > 0, "The configurable lower threshold detects the response");
        const float baseline = sensitive.Background();
        for (int i = 0; i < 50; ++i)
            sensitive.Process(800, 3, alpha, false);
        Check(sensitive.Background() == baseline, "Do not learn a pending response as background");
        sensitive.Reset();
        Check(sensitive.Background() == 0, "Clear baseline on a new capture session");
        FlmSadFilter fresh;
        Check(fresh.Process(20, 3, alpha) == 20, "Different sessions must not share SAD history");
        std::cout << assertions << " native FLM regression assertions passed\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
