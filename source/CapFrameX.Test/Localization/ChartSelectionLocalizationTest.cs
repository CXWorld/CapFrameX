using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Localization;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.Localization
{
    [STATestClass]
    [DoNotParallelize]
    public class ChartSelectionLocalizationTest
    {
        [DataTestMethod]
        [DataRow("en")]
        [DataRow("ru")]
        [DataRow("es")]
        public void DataTabs_RefreshTheSelectedChartInEitherLanguage(string language)
        {
            string previous = CxLang.Instance.UiLanguage;
            try
            {
                CxLang.Instance.SetUiLanguage(language);
                var model = new DataViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(),
                    new EventAggregator(), Configuration(), null, NullLogger<DataViewModel>.Instance);
                var tabs = ReadTabs("DataView.xaml");
                Assert.AreEqual(4, tabs.Count);
                var dirtyFields = new Dictionary<EChartTab, string>
                {
                    [EChartTab.Frametimes] = "_isFrametimeChartDirty",
                    [EChartTab.Fps] = "_isFpsChartDirty",
                    [EChartTab.Distribution] = "_isDistributionChartDirty"
                };
                foreach (var tab in tabs)
                {
                    foreach (string field in dirtyFields.Values)
                        typeof(DataViewModel).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(model, true);
                    model.SelectedChartItem = tab;
                    Assert.AreEqual(tab.Tag, model.SelectedChart);
                    foreach (var pair in dirtyFields)
                    {
                        bool dirty = (bool)typeof(DataViewModel).GetField(pair.Value, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
                        Assert.AreEqual(pair.Key != model.SelectedChart, dirty,
                            $"Selecting {tab.Header} must refresh only {tab.Tag}.");
                    }
                }
            }
            finally
            {
                CxLang.Instance.SetUiLanguage(previous);
            }
        }

        [DataTestMethod]
        [DataRow("en")]
        [DataRow("ru")]
        [DataRow("es")]
        public void ComparisonTabs_KeepChartModeAndColorPickerIndependentOfHeaders(string language)
        {
            string previous = CxLang.Instance.UiLanguage;
            try
            {
                CxLang.Instance.SetUiLanguage(language);
                var logger = new Mock<ILogger<ComparisonViewModel>>();
                var model = new ComparisonViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(),
                    new EventAggregator(), Configuration(), null, logger.Object);
                typeof(ComparisonViewModel).GetMethod("InitializePlotModels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(model, null);
                var tabs = ReadTabs("ComparisonView.xaml");
                Assert.AreEqual(4, tabs.Count);
                foreach (var tab in tabs)
                {
                    model.SelectedChartItem = tab;
                    Assert.AreEqual((EChartTab)tab.Tag == EChartTab.BarCharts, model.IsBarChartTabActive);
                    Assert.AreEqual((EChartTab)tab.Tag == EChartTab.LineCharts, model.IsLineChartTabActive);
                    Assert.AreEqual((EChartTab)tab.Tag == EChartTab.Variances, model.IsVarianceChartTabActive);
                    Assert.AreEqual((EChartTab)tab.Tag == EChartTab.Distribution, model.IsDistributionTabActive);
                    Assert.AreEqual(model.IsLineChartTabActive || model.IsDistributionTabActive, model.ColorPickerVisibility);
                    if (model.IsDistributionTabActive)
                        Assert.IsFalse(model.IsDistributionChartDirty);
                    if (model.IsLineChartTabActive)
                    {
                        Assert.IsFalse(model.IsFpsChartDirty);
                        Assert.IsFalse(model.IsFrametimeChartDirty);
                    }
                }
                logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Never);
            }
            finally
            {
                CxLang.Instance.SetUiLanguage(previous);
            }
        }

        // Read the real XAML declarations so missing or mismatched tags in a view also fail the test.
        private static List<TabItem> ReadTabs(string view)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CapFrameX.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory);
            var document = XDocument.Load(Path.Combine(directory.FullName, "source", "CapFrameX.View", view));
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            return document.Descendants(wpf + "TabItem").Where(tab => tab.Attribute("Tag") != null).Select(tab =>
            {
                var declaration = new XElement(wpf + "TabItem", tab.Attributes().Where(a => a.Name == "Tag" || a.Name == "Header"));
                declaration.Add(document.Root.Attributes().Where(a => a.IsNamespaceDeclaration));
                return (TabItem)XamlReader.Parse(declaration.ToString());
            }).ToList();
        }

        private static CapFrameXConfiguration Configuration()
            => new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, new MemorySettings());

        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
            public Task Load() => Task.CompletedTask;
            public T GetValue<T>(string key) => (T)_values[key];
            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
}
