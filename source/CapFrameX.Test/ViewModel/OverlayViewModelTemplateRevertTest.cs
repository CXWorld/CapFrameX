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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// "Revert" in the overlay template section restores the entries stored when a template was
    /// applied. Before any template had been applied nothing was stored, and the revert replaced
    /// the whole list with that empty state (1.9.0.8 crash reports).
    /// </summary>
    [STATestClass]
    public class OverlayViewModelTemplateRevertTest
    {
        private IOverlayEntry[] _providerEntries;
        private Mock<IOverlayEntryProvider> _provider;
        private OverlayTemplateService _templates;

        [TestInitialize]
        public void Setup()
        {
            _providerEntries = CreateEntries("Profile 1");

            _provider = new Mock<IOverlayEntryProvider>();
            _provider.Setup(provider => provider.GetOverlayEntries(false))
                .Returns(() => Task.FromResult(_providerEntries));

            _templates = new OverlayTemplateService(Mock.Of<ISensorService>());
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

        // The view model marshals the entry reload onto its dispatcher.
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
