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
using System.Windows.Threading;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// "Revert" in the overlay template section restores the entries stored when a template was
    /// applied. Before any template had been applied nothing was stored, and the revert replaced
    /// the whole list with that empty state (1.9.0.8 crash reports). The stored state also belongs
    /// to the profile it was taken from: a revert after a profile switch replaced the new profile's
    /// entries with the old profile's ones.
    /// </summary>
    [STATestClass]
    public class OverlayViewModelTemplateRevertTest
    {
        private IOverlayEntry[] _providerEntries;
        private Mock<IOverlayEntryProvider> _provider;
        private Subject<IOverlayEntry[]> _entriesChanged;
        private OverlayTemplateService _templates;

        [TestInitialize]
        public void Setup()
        {
            _providerEntries = CreateEntries("Profile 1");
            _entriesChanged = new Subject<IOverlayEntry[]>();

            _provider = new Mock<IOverlayEntryProvider>();
            _provider.Setup(provider => provider.GetOverlayEntries(false))
                .Returns(() => Task.FromResult(_providerEntries));
            _provider.Setup(provider => provider.SwitchConfigurationTo(It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            _provider.SetupGet(provider => provider.OverlayEntriesChanged).Returns(_entriesChanged);

            _templates = new OverlayTemplateService(Mock.Of<ISensorService>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            _entriesChanged?.Dispose();
        }

        [TestMethod]
        public void Revert_IsUnavailableUntilATemplateWasApplied()
        {
            var viewModel = CreateViewModel();
            Assert.IsFalse(viewModel.RevertOverlayTemplateCommand.CanExecute(),
                "without an applied template the revert would clear the whole list");

            viewModel.ApplyOverlayTemplateCommand.Execute(null);

            Assert.IsTrue(viewModel.RevertOverlayTemplateCommand.CanExecute());
        }

        [TestMethod]
        public void Apply_BeforeTheEntriesAreLoaded_ChangesNothing()
        {
            _providerEntries = Array.Empty<IOverlayEntry>();
            var viewModel = CreateViewModel();

            viewModel.ApplyOverlayTemplateCommand.Execute(null);

            Assert.IsFalse(_templates.HasStoredState, "an empty list must not become the revert state");
            _provider.Verify(provider => provider.UpdateOverlayEntries(It.IsAny<IEnumerable<IOverlayEntry>>()),
                Times.Never, "an empty list must not replace the provider's entries");
            Assert.IsFalse(viewModel.RevertOverlayTemplateCommand.CanExecute());
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Profile button")]
        [DataRow(true, DisplayName = "Profile hotkey (worker thread)")]
        public void ProfileSwitch_DiscardsTheRevertState(bool fromWorkerThread)
        {
            var viewModel = CreateViewModel();
            viewModel.ApplyOverlayTemplateCommand.Execute(null);
            Assert.IsTrue(viewModel.RevertOverlayTemplateCommand.CanExecute());

            var secondProfile = CreateEntries("Profile 2");
            _providerEntries = secondProfile;
            if (fromWorkerThread)
                Task.Run(() => viewModel.ConfigSwitchCommand.Execute("1")).Wait();
            else
                viewModel.ConfigSwitchCommand.Execute("1");
            PumpUntil(() => viewModel.OverlayEntries.SequenceEqual(secondProfile),
                "the second profile was never shown");

            Assert.IsFalse(_templates.HasStoredState, "the first profile's revert state must be discarded");
            Assert.IsFalse(viewModel.RevertOverlayTemplateCommand.CanExecute());

            // The handler checks the state itself; a revert requested anyway must not bring back
            // the first profile.
            viewModel.RevertOverlayTemplateCommand.Execute();
            CollectionAssert.AreEqual(secondProfile, viewModel.OverlayEntries.ToArray());
        }

        [TestMethod]
        public void DisplayTopologyReload_KeepsTheRevertState()
        {
            var viewModel = CreateViewModel();
            viewModel.ApplyOverlayTemplateCommand.Execute(null);
            var rebuiltEntries = viewModel.OverlayEntries
                .Concat(new[] { new OverlayEntryWrapper("DisplayResolution.2") { GroupName = "Display 2" } })
                .ToArray();

            // A monitor hot-plug rebuilds the list of the same profile.
            _entriesChanged.OnNext(rebuiltEntries);
            PumpUntil(() => viewModel.OverlayEntries.SequenceEqual(rebuiltEntries),
                "the rebuilt list was never shown");

            Assert.IsTrue(viewModel.RevertOverlayTemplateCommand.CanExecute(),
                "the profile did not change, so the template can still be reverted");
        }

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
                _templates,
                hookLearnedProfileService: null,
                hookOverlayStatusService: null);

            var initialEntries = _providerEntries;
            PumpUntil(() => viewModel.OverlayEntries.SequenceEqual(initialEntries),
                "the initial profile was never shown");
            return viewModel;
        }

        private static IOverlayEntry[] CreateEntries(string profile)
            => new IOverlayEntry[]
            {
                new OverlayEntryWrapper("Framerate") { GroupName = profile + " Framerate" },
                new OverlayEntryWrapper("Frametime") { GroupName = profile + " Frametime" }
            };

        // The view model marshals entry reloads and the revert state discard onto its dispatcher.
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
