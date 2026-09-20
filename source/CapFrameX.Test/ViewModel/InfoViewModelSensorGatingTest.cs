using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Test.Mocks;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Reactive.Linq;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The info tab arms <see cref="ISensorConfig.EvaluateAllSensors"/> - and with it the
    /// full sensor evaluation loop - only while its output can actually be seen: the tab
    /// is the active view and the shell is neither minimized nor hidden to the tray.
    /// Window focus must play no role (CapFrameX on a second monitor with a game focused
    /// on the first display keeps updating).
    /// </summary>
    [TestClass]
    public class InfoViewModelSensorGatingTest
    {
        private Mock<ISensorConfig> _sensorConfig;
        private EventAggregator _eventAggregator;
        private InfoViewModel _viewModel;

        [TestInitialize]
        public void Setup()
        {
            _sensorConfig = new Mock<ISensorConfig>();
            _sensorConfig.SetupProperty(config => config.EvaluateAllSensors);

            var appConfiguration = new Mock<IAppConfiguration>();
            appConfiguration.Setup(config => config.OnValueChanged)
                .Returns(Observable.Never<(string key, object value)>());

            _eventAggregator = new EventAggregator();

            _viewModel = new InfoViewModel(
                new MockSensorService(seed: 42),
                _sensorConfig.Object,
                new Mock<ISystemInfo>().Object,
                appConfiguration.Object,
                _eventAggregator,
                new Mock<ILogger<InfoViewModel>>().Object);
        }

        private bool EvaluateAllSensors => _sensorConfig.Object.EvaluateAllSensors;

        private void PublishShellVisibility(bool isContentVisible)
        {
            _eventAggregator.GetEvent<PubSubEvent<AppMessages.ShellVisibilityChanged>>()
                .Publish(new AppMessages.ShellVisibilityChanged(isContentVisible));
        }

        [TestMethod]
        public void Startup_ViewActiveAndShellVisible_TelemetryArmed()
        {
            // The info tab is the startup page; the initial activation raises no
            // OnNavigatedTo, so the constructor itself has to arm the telemetry.
            Assert.IsTrue(EvaluateAllSensors);
        }

        [TestMethod]
        public void ShellMinimized_TelemetryPaused_RestoredOnShow()
        {
            PublishShellVisibility(false);
            Assert.IsFalse(EvaluateAllSensors, "Minimized/hidden shell must pause the telemetry.");

            PublishShellVisibility(true);
            Assert.IsTrue(EvaluateAllSensors, "Restoring the shell must resume the telemetry.");
        }

        [TestMethod]
        public void OtherTabActive_TelemetryPaused_RestoredOnNavigateBack()
        {
            _viewModel.OnNavigatedFrom(null);
            Assert.IsFalse(EvaluateAllSensors, "An inactive info tab must pause the telemetry.");

            _viewModel.OnNavigatedTo(null);
            Assert.IsTrue(EvaluateAllSensors, "Navigating back must resume the telemetry.");
        }

        [TestMethod]
        public void ShellRestored_WhileOtherTabActive_TelemetryStaysPaused()
        {
            _viewModel.OnNavigatedFrom(null);
            PublishShellVisibility(false);
            PublishShellVisibility(true);

            Assert.IsFalse(EvaluateAllSensors,
                "A visible shell alone must not arm the telemetry while another tab is active.");
        }

        [TestMethod]
        public void NavigateBack_WhileShellMinimized_TelemetryStaysPaused()
        {
            PublishShellVisibility(false);
            _viewModel.OnNavigatedFrom(null);
            _viewModel.OnNavigatedTo(null);

            Assert.IsFalse(EvaluateAllSensors,
                "An active info tab alone must not arm the telemetry while the shell is minimized.");
        }
    }
}
