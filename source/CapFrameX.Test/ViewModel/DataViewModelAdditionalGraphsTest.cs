using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    [STATestClass]
    public class DataViewModelAdditionalGraphsTest
    {
        private static readonly (string Selection, string Availability)[] Graphs =
        {
            (nameof(DataViewModel.ShowCpuLoad), nameof(DataViewModel.IsCpuLoadAvailable)),
            (nameof(DataViewModel.ShowCpuMaxThreadLoad), nameof(DataViewModel.IsCpuMaxLoadAvailable)),
            (nameof(DataViewModel.ShowGpuLoad), nameof(DataViewModel.IsGpuLoadAvailable)),
            (nameof(DataViewModel.ShowGpuPowerLimit), nameof(DataViewModel.IsGpuPowerLimitAvailable)),
            (nameof(DataViewModel.ShowPcLatency), nameof(DataViewModel.IsPcLatencyAvailable)),
            (nameof(DataViewModel.ShowAnimationError), nameof(DataViewModel.IsAnimationErrorAvailable)),
            (nameof(DataViewModel.ShowGpuActiveChart), nameof(DataViewModel.IsGpuActiveChartAvailable)),
            (nameof(DataViewModel.ShowCpuActiveChart), nameof(DataViewModel.IsCpuActiveChartAvailable))
        };

        private readonly List<JsonSettingsStorage> _storages = new List<JsonSettingsStorage>();
        private string _configurationFolder;

        [TestInitialize]
        public void Initialize()
        {
            _configurationFolder = Path.Combine(Path.GetTempPath(),
                "cfx-additional-graphs-test-" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var storage in _storages)
            {
                WaitForSave(storage);
            }

            if (Directory.Exists(_configurationFolder))
            {
                Directory.Delete(_configurationFolder, true);
            }
        }

        [TestMethod]
        public void GraphSelections_SurviveSettingsFileAndViewModelReload()
        {
            var first = CreateViewModel(out var firstStorage);
            foreach (var graph in Graphs)
            {
                Set(first, graph.Availability, true);
                Assert.IsFalse(Get(first, graph.Selection), graph.Selection);
                Set(first, graph.Selection, true);
            }
            WaitForSave(firstStorage);

            var restarted = CreateViewModel(out var restartedStorage);
            for (int index = 0; index < Graphs.Length; index++)
            {
                var graph = Graphs[index];
                Set(restarted, graph.Availability, true);
                Assert.IsTrue(Get(restarted, graph.Selection),
                    graph.Selection + " must be restored from AppSettings.json.");
                if (index % 2 == 0)
                {
                    Set(restarted, graph.Selection, false);
                }
            }
            WaitForSave(restartedStorage);

            var restartedAgain = CreateViewModel(out _);
            for (int index = 0; index < Graphs.Length; index++)
            {
                var graph = Graphs[index];
                Set(restartedAgain, graph.Availability, true);
                Assert.AreEqual(index % 2 != 0, Get(restartedAgain, graph.Selection),
                    graph.Selection + " must preserve both enabled and disabled selections.");
            }
        }

        [TestMethod]
        public void MissingMetrics_UpdateBoundTogglesWithoutLosingSelections()
        {
            var viewModel = CreateViewModel(out _);
            foreach (var graph in Graphs)
            {
                Set(viewModel, graph.Availability, true);
                var toggle = new ToggleButton();
                BindingOperations.SetBinding(toggle, ToggleButton.IsCheckedProperty,
                    new Binding(graph.Selection)
                    {
                        Source = viewModel,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });

                toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                Assert.IsTrue(Get(viewModel, graph.Selection), graph.Selection);

                Set(viewModel, graph.Availability, false);
                Assert.IsFalse(Get(viewModel, graph.Selection),
                    graph.Selection + " cannot render without its metric.");
                Assert.IsTrue(toggle.IsChecked == false, graph.Selection);

                Set(viewModel, graph.Availability, true);
                Assert.IsTrue(toggle.IsChecked == true,
                    graph.Selection + " must return when the metric is available again.");

                toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                Set(viewModel, graph.Availability, false);
                Set(viewModel, graph.Availability, true);
                Assert.IsTrue(toggle.IsChecked == false,
                    graph.Selection + " must stay off after the user disables it.");
                BindingOperations.ClearAllBindings(toggle);
            }
        }

        private DataViewModel CreateViewModel(out JsonSettingsStorage storage)
        {
            var paths = new Mock<IPathService>();
            paths.SetupGet(value => value.ConfigFolder).Returns(_configurationFolder);
            storage = new JsonSettingsStorage(NullLogger<JsonSettingsStorage>.Instance, paths.Object);
            _storages.Add(storage);
            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, storage);
            return new DataViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(),
                new EventAggregator(), configuration, null, NullLogger<DataViewModel>.Instance);
        }

        private static bool Get(DataViewModel viewModel, string property)
            => (bool)typeof(DataViewModel).GetProperty(property).GetValue(viewModel);

        private static void Set(DataViewModel viewModel, string property, bool value)
            => typeof(DataViewModel).GetProperty(property).SetValue(viewModel, value);

        private static void WaitForSave(JsonSettingsStorage storage)
            => storage.WaitForPendingSaveAsync().WaitAsync(TimeSpan.FromSeconds(10))
                .GetAwaiter().GetResult();
    }
}
