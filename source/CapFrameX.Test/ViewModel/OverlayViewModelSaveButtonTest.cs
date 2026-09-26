using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hardware.Controller;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Test.Mocks;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// An edit on the overlay page has to enable the save button, also after the profile was
    /// switched: the switch reloads the entries, and the reloaded ones must report their edits.
    /// </summary>
    [STATestClass]
    public class OverlayViewModelSaveButtonTest
    {
        private string _configFolder;
        private MockSensorService _sensorService;
        private Mock<IAppConfiguration> _configuration;
        private Subject<(string key, object value)> _configurationChanges;
        private OverlayEntryProvider _provider;

        [TestInitialize]
        public void Setup()
        {
            _configFolder = Path.Combine(Path.GetTempPath(), "CxTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_configFolder);

            _sensorService = new MockSensorService(seed: 42);

            var core = new OverlayEntryCore();
            core.OverlayEntryCoreCompletionSource.SetResult(true);

            _configurationChanges = new Subject<(string key, object value)>();
            _configuration = new Mock<IAppConfiguration>();
            _configuration.SetupAllProperties();
            _configuration.Object.OverlayEntryConfigurationFile = 0;
            _configuration.Object.HardwareInfoSource = "Auto";
            _configuration.SetupGet(config => config.OnValueChanged).Returns(_configurationChanges);

            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator.Setup(aggregator => aggregator.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            var rtss = new Mock<IRTSSService>();
            rtss.Setup(service => service.ProcessIdStream).Returns(new BehaviorSubject<int>(0));
            rtss.Setup(service => service.GetCurrentFramerate(It.IsAny<int>())).Returns(Tuple.Create(0.0, 0.0));

            var paths = new Mock<IPathService>();
            paths.Setup(service => service.ConfigFolder).Returns(_configFolder);

            _provider = new OverlayEntryProvider(
                _sensorService,
                _configuration.Object,
                eventAggregator.Object,
                Mock.Of<IOnlineMetricService>(),
                Mock.Of<ISystemInfo>(),
                rtss.Object,
                Mock.Of<ISensorConfig>(),
                core,
                Mock.Of<IThreadAffinityController>(),
                paths.Object,
                hookOverlayStatusService: null,
                Mock.Of<ILogger<OverlayEntryProvider>>(),
                () => Array.Empty<DetectedDisplay>());

            // Two saved profiles, so the switch loads real JSON instead of the defaults.
            _provider.GetOverlayEntries(false).Wait();
            _provider.SaveOverlayEntriesToJson(0).Wait();
            _provider.SaveOverlayEntriesToJson(1).Wait();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _sensorService?.Dispose();
            _configurationChanges?.Dispose();
            try { Directory.Delete(_configFolder, true); } catch { }
        }

        [TestMethod]
        public void EditBeforeSwitch_EnablesSave()
        {
            var viewModel = CreateViewModel();

            Toggle(viewModel.OverlayEntries.First());

            Assert.IsTrue(viewModel.SaveButtonIsEnable);
        }

        [TestMethod]
        [DataRow("1", DisplayName = "Switch to profile 2")]
        [DataRow("0", DisplayName = "Switch to the active profile")]
        public void EditAfterSwitch_EnablesSave(string profile)
        {
            var viewModel = CreateViewModel();
            var before = viewModel.OverlayEntries.ToArray();

            viewModel.ConfigSwitchCommand.Execute(profile);
            PumpUntil(() => !viewModel.OverlayEntries.SequenceEqual(before), "the switched profile was never shown");
            PumpFor(TimeSpan.FromMilliseconds(300));
            Assert.IsFalse(viewModel.SaveButtonIsEnable, "a freshly loaded profile has nothing to save");

            Toggle(viewModel.OverlayEntries.First());

            Assert.IsTrue(viewModel.SaveButtonIsEnable, "the edit after the switch did not enable the save button");
        }

        [TestMethod]
        public void EditAfterTemplateAndSave_EnablesSave()
        {
            // Applying a template replaces the entries with templated clones.
            var viewModel = CreateViewModel();
            viewModel.ApplyOverlayTemplateCommand.Execute(null);
            Save(viewModel);

            Toggle(viewModel.OverlayEntries.First());

            Assert.IsTrue(viewModel.SaveButtonIsEnable, "the edit on a templated entry did not enable the save button");
        }

        [TestMethod]
        public void SwitchProfileApplyTemplateSaveThenEdit_EnablesSave()
        {
            // The reported sequence: profile 2 -> 1, load a template, save, keep editing.
            _provider.SwitchConfigurationTo(1).Wait();
            var viewModel = CreateViewModel();
            var profile2 = viewModel.OverlayEntries.ToArray();

            viewModel.ConfigSwitchCommand.Execute("0");
            PumpUntil(() => !viewModel.OverlayEntries.SequenceEqual(profile2), "profile 1 was never shown");
            PumpFor(TimeSpan.FromMilliseconds(300));

            viewModel.SelectedOverlayTemplate = EOverlayTemplate.Detailed;
            viewModel.ApplyOverlayTemplateCommand.Execute(null);
            Save(viewModel);

            Toggle(viewModel.OverlayEntries.First());

            Assert.IsTrue(viewModel.SaveButtonIsEnable, "the edit after template and save did not enable the save button");
        }

        [TestMethod]
        public void EditAfterTemplateRevertAndSave_EnablesSave()
        {
            // Reverting restores the entries stored when the template was applied.
            var viewModel = CreateViewModel();
            viewModel.ApplyOverlayTemplateCommand.Execute(null);
            viewModel.RevertOverlayTemplateCommand.Execute();
            Save(viewModel);

            Toggle(viewModel.OverlayEntries.First());

            Assert.IsTrue(viewModel.SaveButtonIsEnable, "the edit on a restored entry did not enable the save button");
        }

        [TestMethod]
        public void GroupRenameAfterTemplate_UpdatesSeparatorList()
        {
            var viewModel = CreateViewModel();
            viewModel.ApplyOverlayTemplateCommand.Execute(null);

            var entry = viewModel.OverlayEntries.First(candidate => !string.IsNullOrWhiteSpace(candidate.GroupName));
            entry.GroupName = "Renamed group";

            Assert.IsTrue(viewModel.OverlaySubModelGroupSeparating.OverlayGroupNameSeparatorEntries
                .Any(separator => separator.GroupName == "Renamed group"),
                "a renamed group on a templated entry did not reach the separator list");
        }

        private static void Save(OverlayViewModel viewModel)
        {
            viewModel.SaveConfigCommand.Execute("3");
            PumpUntil(() => !viewModel.SaveButtonIsEnable, "saving did not reset the save button");
        }

        private static void Toggle(IOverlayEntry entry) => entry.ShowOnOverlay = !entry.ShowOnOverlay;

        private OverlayViewModel CreateViewModel()
        {
            var overlayService = new Mock<IOverlayService>();

            var viewModel = new OverlayViewModel(
                overlayService.Object,
                _provider,
                _configuration.Object,
                Mock.Of<IPathService>(),
                _sensorService,
                Mock.Of<IRTSSService>(),
                Mock.Of<IThreadAffinityController>(),
                Mock.Of<IOnlineMetricService>(),
                new OverlayTemplateService(_sensorService),
                hookLearnedProfileService: null,
                hookOverlayStatusService: null);

            PumpUntil(() => viewModel.OverlayEntries.Any(), "the initial profile was never shown");
            PumpFor(TimeSpan.FromMilliseconds(300));
            return viewModel;
        }

        private static void PumpUntil(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (true)
            {
                PumpOnce();
                if (condition())
                    return;
                if (DateTime.UtcNow > deadline)
                    Assert.Fail(message);
                Thread.Sleep(10);
            }
        }

        private static void PumpFor(TimeSpan duration)
        {
            var end = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < end)
            {
                PumpOnce();
                Thread.Sleep(10);
            }
        }

        private static void PumpOnce()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}
