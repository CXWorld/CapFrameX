using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookMetricsPublisherTest
    {
        // Before its overlay gate, OnEntries touches nothing but the target policy, which records
        // its reason even without a target PID. A reason no policy check produces therefore shows
        // whether an entry update was handled.
        private const string UntouchedReason = "untouched by the policy";

        [TestMethod]
        public void EntriesWhileTheOverlayIsSwitchedOff_AreIgnored()
        {
            using var entries = new Subject<IOverlayEntry[]>();
            using var configChanges = new Subject<(string key, object value)>();
            var config = new Mock<IAppConfiguration>();
            config.SetupGet(x => x.OnValueChanged).Returns(configChanges);
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(x => x.OnDictionaryUpdated).Returns(entries);
            using var publisher = new HookMetricsPublisher(overlay.Object, config.Object, Observable.Never<int>());

            // A remote API client keeps the entries flowing while the overlay is switched off.
            SetField(publisher, "_lastPolicyReason", UntouchedReason);
            entries.OnNext(Array.Empty<IOverlayEntry>());
            Assert.AreEqual(UntouchedReason, ReadField<string>(publisher, "_lastPolicyReason"),
                "The hidden hook must not re-check its target policy for remote API updates.");

            configChanges.OnNext((nameof(IAppConfiguration.IsOverlayActive), true));
            SetField(publisher, "_lastPolicyReason", UntouchedReason);
            entries.OnNext(Array.Empty<IOverlayEntry>());
            Assert.AreNotEqual(UntouchedReason, ReadField<string>(publisher, "_lastPolicyReason"),
                "With the overlay on, every update re-checks the target policy.");
        }

        private static T ReadField<T>(object target, string name) =>
            (T)Field(target, name).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            Field(target, name).SetValue(target, value);

        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field: {name}");
    }
}
