using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class OverlayPercentageBarTest
    {
        [TestMethod]
        public void LegacyProfile_DefaultsToTextAndPersistsOnlyTheSelectedMode()
        {
            var entry = JsonConvert.DeserializeObject<OverlayEntryWrapper>("{\"Identifier\":\"GpuLoad\"}");
            Assert.AreEqual(EOverlayValueDisplayMode.Text, entry.ValueDisplayMode);

            entry.ValueUnitFormat = "%";
            entry.ValueDisplayMode = EOverlayValueDisplayMode.TextAndBar;
            var json = JObject.Parse(JsonConvert.SerializeObject(entry));
            var restored = json.ToObject<OverlayEntryWrapper>();

            Assert.AreEqual(EOverlayValueDisplayMode.TextAndBar, restored.ValueDisplayMode);
            Assert.IsNull(json[nameof(OverlayEntryWrapper.SupportsPercentageBar)]);
            Assert.IsNull(json[nameof(OverlayEntryWrapper.ValueUnitFormat)]);
            Assert.AreEqual(EOverlayValueDisplayMode.TextAndBar, entry.Clone().ValueDisplayMode);
        }

        [TestMethod]
        public void Eligibility_UsesTheActualUnitAndWorksBeforeTheFirstSample()
        {
            var entry = new OverlayEntryWrapper("1%Low")
            {
                Description = "1% Low FPS",
                ValueUnitFormat = "FPS",
                ValueDisplayMode = EOverlayValueDisplayMode.TextAndBar
            };
            Assert.IsFalse(entry.SupportsPercentageBar);
            Assert.AreEqual(EOverlayValueDisplayMode.Text, OverlayPercentageBar.GetDisplayMode(entry));

            entry.ValueUnitFormat = "%  ";
            Assert.IsTrue(entry.SupportsPercentageBar);
            Assert.AreEqual(EOverlayValueDisplayMode.TextAndBar, OverlayPercentageBar.GetDisplayMode(entry));
            Assert.IsFalse(OverlayPercentageBar.TryGetValue(entry, out _));

            entry.ValueDisplayMode = (EOverlayValueDisplayMode)99;
            Assert.AreEqual(EOverlayValueDisplayMode.Text, OverlayPercentageBar.GetDisplayMode(entry));
        }

        [TestMethod]
        public void UnitRefresh_NotifiesCapabilityWithoutDirtyingTheProfile()
        {
            var entry = new OverlayEntryWrapper("GpuLoad");
            var notifications = new List<string>();
            int dirtyCount = 0;
            entry.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
            entry.PropertyChangedAction = () => dirtyCount++;

            entry.ValueUnitFormat = "%";
            entry.ValueUnitFormat = "%";
            CollectionAssert.AreEqual(new[] { nameof(entry.SupportsPercentageBar) }, notifications);
            Assert.AreEqual(0, dirtyCount);

            entry.ValueDisplayMode = EOverlayValueDisplayMode.TextAndBar;
            Assert.AreEqual(1, dirtyCount);
            Assert.IsTrue(entry.FormatChanged);
        }

        [TestMethod]
        public void NumericValue_RejectsMissingAndNonfiniteValuesAndRetainsOverflowForText()
        {
            var entry = new OverlayEntryWrapper("GpuLoad");
            foreach (var invalid in new object[] { null, true, '5', "N/A", double.NaN,
                double.PositiveInfinity, double.NegativeInfinity, DateTime.UtcNow, EOverlayValueDisplayMode.Bar })
            {
                entry.Value = invalid;
                Assert.IsFalse(OverlayPercentageBar.TryGetValue(entry, out _), $"Accepted {invalid}");
            }

            entry.Value = 123.5m;
            Assert.IsTrue(OverlayPercentageBar.TryGetValue(entry, out var value));
            Assert.AreEqual(123.5, value);
            entry.Value = -5;
            Assert.IsTrue(OverlayPercentageBar.TryGetValue(entry, out value));
            Assert.AreEqual(-5d, value);
        }

        [TestMethod]
        public void NumericStrings_ParseInvariantAndCurrentCultureWithoutThousandsAmbiguity()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                var entry = new OverlayEntryWrapper("GpuLoad");
                foreach (var text in new[] { "72.5", "72,5" })
                {
                    entry.Value = text;
                    Assert.IsTrue(OverlayPercentageBar.TryGetValue(entry, out var value));
                    Assert.AreEqual(72.5, value);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        public void LimitColors_IncludeBoundariesAndGiveUpperLimitPriority()
        {
            var entry = new OverlayEntryWrapper("GpuLoad")
            {
                Color = "123456", UpperLimitColor = "FF0000", LowerLimitColor = "0000FF",
                UpperLimitValue = "90.5", LowerLimitValue = "20.5", Value = 90.5
            };
            Assert.AreEqual("FF0000", OverlayPercentageBar.GetColor(entry));
            entry.Value = 20.5;
            Assert.AreEqual("0000FF", OverlayPercentageBar.GetColor(entry));
            entry.Value = 50;
            Assert.AreEqual("123456", OverlayPercentageBar.GetColor(entry));
            entry.LowerLimitValue = "100";
            entry.Value = 95;
            Assert.AreEqual("FF0000", OverlayPercentageBar.GetColor(entry));
        }

        [TestMethod]
        public void TemplateRevert_RestoresTheStoredDisplayMode()
        {
            var service = new OverlayTemplateService(Mock.Of<ISensorService>());
            var entry = new OverlayEntryWrapper("GpuLoad")
            {
                ValueUnitFormat = "%", ValueDisplayMode = EOverlayValueDisplayMode.TextAndBar
            };
            service.StoreCurrentState(new[] { entry });
            entry.ValueDisplayMode = EOverlayValueDisplayMode.Text;

            var restored = service.GetStoredOverlayEntries().Single();
            Assert.AreEqual(EOverlayValueDisplayMode.TextAndBar, restored.ValueDisplayMode);
            Assert.IsTrue(restored.SupportsPercentageBar);
        }
    }
}
