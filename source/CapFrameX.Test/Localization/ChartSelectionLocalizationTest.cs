using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
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
            using var subscriptions = new CxLangSubscriptions();
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
            using var subscriptions = new CxLangSubscriptions();
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

        // Axis titles used to be a translated source name plus a hard-coded unit or "Distribution" suffix,
        // which left "[ms]" and English word order in other languages.
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ComparisonAxisTitles_ComeWholeFromTheCatalog(bool displayTimes)
        {
            using var subscriptions = new CxLangSubscriptions();
            string previous = CxLang.Instance.UiLanguage;
            try
            {
                CxLang.Instance.SetUiLanguage("ru");
                var model = new ComparisonViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(),
                    new EventAggregator(), Configuration(), null, Mock.Of<ILogger<ComparisonViewModel>>());
                typeof(ComparisonViewModel).GetMethod("InitializePlotModels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(model, null);
                typeof(ComparisonViewModel).GetField("_useDisplayChangeSamplesForComparison", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(model, displayTimes);
                typeof(ComparisonViewModel).GetMethod("UpdateComparisonMetricSourceLabels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(model, null);

                string source = displayTimes ? "DisplayTime" : "PresentFrametime";
                Assert.AreEqual(CxLang.T($"ComparisonViewModel_{source}Distribution"),
                    model.ComparisonDistributionModel.Axes.Single(axis => axis.Key == "yAxis").Title);
                Assert.AreEqual(CxLang.T($"ComparisonViewModel_{source}Ms"),
                    model.ComparisonDistributionModel.Axes.Single(axis => axis.Key == "xAxis").Title);

                var titles = new[] { model.ComparisonFrametimesModel, model.ComparisonFpsModel, model.ComparisonDistributionModel }
                    .SelectMany(plot => plot.Axes).Select(axis => axis.Title).Append(model.ComparisonLShapeYAxisLabel);
                foreach (string title in titles.Where(t => t != null))
                    Assert.IsFalse(title.Contains("[ms]") || title.Contains("(ms)") || title.Contains("[1/s]"),
                        $"'{title}' keeps an English unit.");
            }
            finally
            {
                CxLang.Instance.SetUiLanguage(previous);
            }
        }

        // The Values selectors show translated item texts, but the view models, the chart manager and the
        // Comparison view's triggers compare SelectedChartView with the English keys.
        [DataTestMethod]
        [DataRow("ComparisonView.xaml", "en")]
        [DataRow("ComparisonView.xaml", "ru")]
        [DataRow("PmdView.xaml", "en")]
        [DataRow("PmdView.xaml", "ru")]
        public void ValuesSelectors_PassTheLanguageIndependentKey(string view, string language)
        {
            string previous = CxLang.Instance.UiLanguage;
            try
            {
                CxLang.Instance.SetUiLanguage(language);
                var selector = ReadChartViewSelector(view);
                var model = new ChartViewModel();
                selector.DataContext = model;
                DrainBindings();

                var items = selector.Items.Cast<ComboBoxItem>().ToList();
                CollectionAssert.AreEqual(new[] { "Frametimes", "FPS" }, items.Select(item => item.Tag).ToArray());
                Assert.AreEqual("Frametimes", ((ComboBoxItem)selector.SelectedItem).Tag, "The model's default must select its item.");
                if (language != "en")
                    Assert.AreNotEqual("Frametimes", items[0].Content, "The item text is expected to be translated.");

                foreach (var item in items)
                {
                    selector.SelectedItem = item;
                    DrainBindings();
                    Assert.AreEqual(item.Tag, model.SelectedChartView, $"Selecting '{item.Content}' in {view}.");
                }

                model.SelectedChartView = "Frametimes";
                DrainBindings();
                Assert.AreSame(items[0], selector.SelectedItem);
            }
            finally
            {
                CxLang.Instance.SetUiLanguage(previous);
            }
        }

        private static ComboBox ReadChartViewSelector(string view)
        {
            var document = LoadView(view);
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var selector = document.Descendants(wpf + "ComboBox")
                .Single(box => ((string)box.Attribute("SelectedValue"))?.Contains("SelectedChartView") == true);
            var declaration = new XElement(wpf + "ComboBox",
                selector.Attributes().Where(a => a.Name == "SelectedValuePath" || a.Name == "SelectedValue"),
                selector.Elements(wpf + "ComboBoxItem"));
            declaration.Add(document.Root.Attributes().Where(a => a.IsNamespaceDeclaration));
            return (ComboBox)XamlReader.Parse(declaration.ToString());
        }

        private static void DrainBindings()
            => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        private sealed class ChartViewModel : INotifyPropertyChanged
        {
            private string _selectedChartView = "Frametimes";

            public event PropertyChangedEventHandler PropertyChanged;

            public string SelectedChartView
            {
                get => _selectedChartView;
                set
                {
                    _selectedChartView = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedChartView)));
                }
            }
        }

        // Read the real XAML declarations so missing or mismatched tags in a view also fail the test.
        private static List<TabItem> ReadTabs(string view)
        {
            var document = LoadView(view);
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            return document.Descendants(wpf + "TabItem").Where(tab => tab.Attribute("Tag") != null).Select(tab =>
            {
                var declaration = new XElement(wpf + "TabItem", tab.Attributes().Where(a => a.Name == "Tag" || a.Name == "Header"));
                declaration.Add(document.Root.Attributes().Where(a => a.IsNamespaceDeclaration));
                return (TabItem)XamlReader.Parse(declaration.ToString());
            }).ToList();
        }

        private static XDocument LoadView(string view)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CapFrameX.sln")))
                directory = directory.Parent;
            Assert.IsNotNull(directory);
            return XDocument.Load(Path.Combine(directory.FullName, "source", "CapFrameX.View", view));
        }

        private static CapFrameXConfiguration Configuration()
            => new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, new MemorySettings());

        // The view models read settings from background work while their constructor still runs.
        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>();
            public Task Load() => Task.CompletedTask;
            public T GetValue<T>(string key) => (T)_values[key];
            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
}
