using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Hardware.Controller;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The format buttons of the overlay page copy the selected entry's format to other entries or
    /// reset it. Clicked without a selected entry they threw a NullReferenceException in
    /// OverlayEntryProvider.SetFormatForAllGroups (1.9.1.5 crash report); they have to stay
    /// unavailable until an entry is selected.
    /// </summary>
    [STATestClass]
    public class OverlayViewModelFormatCommandTest
    {
        private IOverlayEntry[] _providerEntries;
        private Mock<IOverlayEntryProvider> _provider;
        private Subject<IOverlayEntry[]> _entriesChanged;

        [TestInitialize]
        public void Setup()
        {
            _providerEntries = new IOverlayEntry[]
            {
                new OverlayEntryWrapper("Framerate") { GroupName = "Framerate" },
                new OverlayEntryWrapper("Frametime") { GroupName = "Frametime" }
            };
            _entriesChanged = new Subject<IOverlayEntry[]>();

            _provider = new Mock<IOverlayEntryProvider>();
            _provider.Setup(provider => provider.GetOverlayEntries(false))
                .Returns(() => Task.FromResult(_providerEntries));
            _provider.SetupGet(provider => provider.OverlayEntriesChanged).Returns(_entriesChanged);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _entriesChanged?.Dispose();
        }

        [TestMethod]
        public void FormatCommands_AreUnavailableWithoutSelectedEntry()
        {
            var viewModel = CreateViewModel();

            foreach (var command in FormatCommands(viewModel))
                Assert.IsFalse(command.Value.CanExecute(null), command.Key + " is available without a selected entry");
        }

        [TestMethod]
        public void FormatCommands_FollowTheSelection()
        {
            var viewModel = CreateViewModel();
            var changed = FormatCommands(viewModel).Keys.ToDictionary(name => name, name => false);
            foreach (var command in FormatCommands(viewModel))
                command.Value.CanExecuteChanged += (sender, args) => changed[command.Key] = true;

            viewModel.SelectedOverlayEntry = viewModel.OverlayEntries.First();

            // Prism raises CanExecuteChanged through the synchronization context it was created on.
            PumpUntil(() => changed.Values.All(raised => raised),
                "a format command did not tell its button about the selection");
            foreach (var command in FormatCommands(viewModel))
                Assert.IsTrue(command.Value.CanExecute(null), command.Key + " is unavailable with a selected entry");

            // The list reload of a profile switch clears the selection again.
            viewModel.SelectedOverlayEntry = null;

            foreach (var command in FormatCommands(viewModel))
                Assert.IsFalse(command.Value.CanExecute(null), command.Key + " stayed available after the selection was cleared");
        }

        [TestMethod]
        public void SetFormatForAllGroups_CopiesFromTheSelectedEntry()
        {
            var viewModel = CreateViewModel();
            var selected = viewModel.OverlayEntries.First();
            viewModel.SelectedOverlayEntry = selected;

            viewModel.SetFormatForAllGroupsCommand.Execute(null);

            _provider.Verify(provider => provider.SetFormatForAllGroups(selected, viewModel.Checkboxes), Times.Once);
        }

        private static IReadOnlyDictionary<string, ICommand> FormatCommands(OverlayViewModel viewModel)
            => new Dictionary<string, ICommand>
            {
                [nameof(OverlayViewModel.SetFormatForGroupNameCommand)] = viewModel.SetFormatForGroupNameCommand,
                [nameof(OverlayViewModel.SetFormatForSensorTypeCommand)] = viewModel.SetFormatForSensorTypeCommand,
                [nameof(OverlayViewModel.ResetColorAndLimitDefaultsCommand)] = viewModel.ResetColorAndLimitDefaultsCommand,
                [nameof(OverlayViewModel.SetFormatForAllGroupsCommand)] = viewModel.SetFormatForAllGroupsCommand,
                [nameof(OverlayViewModel.SetFormatForAllValuesCommand)] = viewModel.SetFormatForAllValuesCommand,
            };

        private OverlayViewModel CreateViewModel()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            configuration.SetupGet(config => config.OnValueChanged)
                .Returns(Observable.Never<(string key, object value)>());

            var viewModel = new OverlayViewModel(
                Mock.Of<IOverlayService>(),
                _provider.Object,
                configuration.Object,
                Mock.Of<IPathService>(),
                Mock.Of<ISensorService>(),
                Mock.Of<IRTSSService>(),
                Mock.Of<IThreadAffinityController>(),
                Mock.Of<IOnlineMetricService>(),
                new OverlayTemplateService(Mock.Of<ISensorService>()),
                hookLearnedProfileService: null,
                hookOverlayStatusService: null);

            var initialEntries = _providerEntries;
            PumpUntil(() => viewModel.OverlayEntries.SequenceEqual(initialEntries),
                "the initial profile was never shown");
            return viewModel;
        }

        // The view model marshals entry loads onto its dispatcher.
        private static void PumpUntil(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (true)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                if (condition())
                    return;
                if (DateTime.UtcNow > deadline)
                    Assert.Fail(message);
                Thread.Sleep(10);
            }
        }
    }
}
