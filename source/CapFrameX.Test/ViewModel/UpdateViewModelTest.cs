using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Update;
using CapFrameX.ViewModel;
using DryIoc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// A download the user confirmed ends in a restart into the installer: right away, or once a
    /// running capture has been saved.
    /// </summary>
    [STATestClass]
    public class UpdateViewModelTest
    {
        private static readonly UpdatePackageInfo Package = new UpdatePackageInfo
        {
            Version = new Version(1, 9, 3, 0),
            Channel = EUpdateChannel.Release,
            PackageUri = new Uri("https://updates.capframex.com/packages/1.9.3.0/CapFrameXInstaller.msi"),
            Sha256 = new string('a', 64),
            SizeInBytes = 100
        };

        private BehaviorSubject<UpdateStatus> _status;
        private Mock<IUpdateService> _updateService;
        private UpdateStatus _downloadResult;
        private bool _isCaptureBusy;
        private int _shutdowns;

        [TestInitialize]
        public void Setup()
        {
            _status = new BehaviorSubject<UpdateStatus>(new UpdateStatus(EUpdateState.UpdateAvailable, Package));
            _downloadResult = new UpdateStatus(EUpdateState.ReadyToInstall, Package, 1d);
            _isCaptureBusy = false;
            _shutdowns = 0;

            _updateService = new Mock<IUpdateService>();
            _updateService.SetupGet(service => service.IsConfigured).Returns(true);
            _updateService.SetupGet(service => service.StatusStream).Returns(_status);
            _updateService.SetupGet(service => service.AvailablePackages).Returns(new[] { Package });
            _updateService.SetupGet(service => service.MinimumRollbackVersion).Returns(new Version(1, 9, 0, 0));
            _updateService.Setup(service => service.StartInstallAfterExit()).Returns(true);
            // Like the real service: the status stream reports the result before the task completes.
            _updateService.Setup(service => service.DownloadUpdateAsync(It.IsAny<CancellationToken>())).Returns(() =>
            {
                _status.OnNext(_downloadResult);
                return Task.FromResult(_downloadResult);
            });
        }

        [TestMethod]
        public void ConfirmedUpdate_RestartsIntoTheInstallerOnceDownloaded()
        {
            var viewModel = CreateViewModel();

            viewModel.ConfirmUpdateCommand.Execute(null);

            _updateService.Verify(service => service.StartInstallAfterExit(), Times.Once);
            Assert.AreEqual(1, _shutdowns);
            StringAssert.Contains(viewModel.StatusText, "1.9.3.0");
        }

        [TestMethod]
        public void RunningCapture_DefersTheRestartUntilItIsSaved()
        {
            _isCaptureBusy = true;
            var viewModel = CreateViewModel();

            viewModel.ConfirmUpdateCommand.Execute(null);

            _updateService.Verify(service => service.StartInstallAfterExit(), Times.Never);
            Assert.AreEqual(0, _shutdowns, "A running capture must not be cut off.");
            var waitingText = viewModel.StatusText;
            StringAssert.Contains(waitingText, "1.9.3.0");

            Assert.IsFalse(viewModel.TryInstallStagedUpdate(), "Still capturing.");
            _isCaptureBusy = false;
            Assert.IsTrue(viewModel.TryInstallStagedUpdate());

            _updateService.Verify(service => service.StartInstallAfterExit(), Times.Once);
            Assert.AreEqual(1, _shutdowns);
            Assert.AreNotEqual(waitingText, viewModel.StatusText);
        }

        [TestMethod]
        public void NewInstanceNotStarted_KeepsTheAppRunning()
        {
            _updateService.Setup(service => service.StartInstallAfterExit()).Returns(false);
            var viewModel = CreateViewModel();

            viewModel.ConfirmUpdateCommand.Execute(null);

            Assert.AreEqual(0, _shutdowns, "Without a new instance the app must stay; the update installs on the next start.");
        }

        [TestMethod]
        public void FailedDownload_DoesNotRestart()
        {
            _downloadResult = new UpdateStatus(EUpdateState.Failed, Package, message: "checksum");
            var viewModel = CreateViewModel();

            viewModel.ConfirmUpdateCommand.Execute(null);

            _updateService.Verify(service => service.StartInstallAfterExit(), Times.Never);
            Assert.AreEqual(0, _shutdowns);
        }

        [TestMethod]
        public void StagedUpdateReplacedWhileWaiting_IsLeftToTheNextStart()
        {
            _isCaptureBusy = true;
            var viewModel = CreateViewModel();
            viewModel.ConfirmUpdateCommand.Execute(null);

            _status.OnNext(new UpdateStatus(EUpdateState.Checking));
            _isCaptureBusy = false;

            Assert.IsTrue(viewModel.TryInstallStagedUpdate(), "The wait ends without a restart.");
            _updateService.Verify(service => service.StartInstallAfterExit(), Times.Never);
            Assert.AreEqual(0, _shutdowns);
        }

        [TestMethod]
        public void Container_UsesThePublicConstructorAndDoesNotResolveTheCaptureManagerEarly()
        {
            // Mirrors the Prism rules: the most resolvable public constructor, lazy wrappers without
            // registration. CaptureManager is deliberately not registered.
            using (var container = new Container(rules => rules
                .With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithFuncAndLazyWithoutRegistration()))
            {
                container.RegisterInstance(_updateService.Object);
                container.RegisterInstance(Configuration());
                container.RegisterInstance(VersionProvider());
                container.RegisterInstance(Mock.Of<ILogger<UpdateViewModel>>());
                container.Register<UpdateViewModel>(Reuse.Singleton);

                Assert.IsNotNull(container.Resolve<UpdateViewModel>());
            }
        }

        private UpdateViewModel CreateViewModel()
            => new UpdateViewModel(_updateService.Object, Configuration(), VersionProvider(),
                Mock.Of<ILogger<UpdateViewModel>>(), () => _isCaptureBusy, () => _shutdowns++);

        private static IAppConfiguration Configuration()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupGet(appConfiguration => appConfiguration.AutoUpdateCheckActive).Returns(false);
            return configuration.Object;
        }

        private static IAppVersionProvider VersionProvider()
        {
            var versionProvider = new Mock<IAppVersionProvider>();
            versionProvider.Setup(provider => provider.GetAppVersion()).Returns(new Version(1, 9, 2, 0));
            versionProvider.Setup(provider => provider.GetReleaseChannel()).Returns(EUpdateChannel.Release);
            return versionProvider.Object;
        }
    }
}
