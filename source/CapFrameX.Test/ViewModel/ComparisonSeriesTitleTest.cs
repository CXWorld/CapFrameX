using CapFrameX.Extensions;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;
using OxyPlot.Series;
using System.Collections.Generic;
using System.Reflection;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class ComparisonSeriesTitleTest
    {
        private static void ApplySeriesTitles(PlotModel model, IDictionary<string, string> titles)
        {
            var method = typeof(ComparisonViewModel).GetMethod("ApplySeriesTitles",
                BindingFlags.NonPublic | BindingFlags.Static);

            method.Invoke(null, new object[] { model, titles });
        }

        private static PlotModel CreateModel(params string[] recordIds)
        {
            var model = new PlotModel();

            foreach (string recordId in recordIds)
                model.Series.Add(new LineSeries { Tag = recordId, Title = string.Empty });

            return model;
        }

        [TestMethod]
        public void ApplySeriesTitles_SeriesInRecordOrder_LabelsEachSeries()
        {
            var model = CreateModel("a", "b", "c");
            var titles = new Dictionary<string, string> { ["a"] = "A", ["b"] = "B", ["c"] = "C" };

            ApplySeriesTitles(model, titles);

            Assert.AreEqual("A", model.Series[0].Title);
            Assert.AreEqual("B", model.Series[1].Title);
            Assert.AreEqual("C", model.Series[2].Title);
        }

        [TestMethod]
        public void ApplySeriesTitles_AfterBringToFront_LabelStaysWithItsRecord()
        {
            var model = CreateModel("a", "b", "c");

            // Hovering a record moves its series to the end so it draws on top; the collection is
            // never put back in record order. The label must follow the record, not the position.
            model.Series.Move(0, model.Series.Count - 1);

            ApplySeriesTitles(model,
                new Dictionary<string, string> { ["a"] = "A", ["b"] = "B", ["c"] = "C" });

            Assert.AreEqual("b", model.Series[0].Tag);
            Assert.AreEqual("B", model.Series[0].Title);
            Assert.AreEqual("c", model.Series[1].Tag);
            Assert.AreEqual("C", model.Series[1].Title);
            Assert.AreEqual("a", model.Series[2].Tag);
            Assert.AreEqual("A", model.Series[2].Title);
        }

        [TestMethod]
        public void ApplySeriesTitles_HiddenRecordOmitted_KeepsOtherTitlesWithTheirRecord()
        {
            var model = CreateModel("a", "b", "c");
            model.Series[1].Title = string.Empty;
            model.Series.Move(2, model.Series.Count - 1);

            // A hidden record contributes no title, so its series keeps the empty title the hide
            // toggle gave it and must not consume another record's label.
            ApplySeriesTitles(model,
                new Dictionary<string, string> { ["a"] = "A", ["c"] = "C" });

            Assert.AreEqual("A", model.Series[0].Title);
            Assert.AreEqual(string.Empty, model.Series[1].Title);
            Assert.AreEqual("C", model.Series[2].Title);
        }

        [TestMethod]
        public void ApplySeriesTitles_StaleSeriesWithoutLabel_IsLeftUntouched()
        {
            var model = CreateModel("a", "removed");
            model.Series[1].Title = "stale";

            ApplySeriesTitles(model, new Dictionary<string, string> { ["a"] = "A" });

            Assert.AreEqual("A", model.Series[0].Title);
            Assert.AreEqual("stale", model.Series[1].Title);
        }
    }
}
