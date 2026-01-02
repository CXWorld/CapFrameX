using CapFrameX.Capture.Contracts;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.MVVM;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Contracts.UpdateCheck;
using CapFrameX.Data;
using CapFrameX.Data.Logging;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Hardware.Controller;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.PresentMonInterface;
using CapFrameX.RTSSIntegration;
using CapFrameX.Sensor;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Updater;
using DryIoc;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Events;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Regions;
using Serilog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX
{
    public class Bootstrapper : DryIocBootstrapper
    {
		protected override DependencyObject CreateShell()
        {
            var shell = Container.Resolve<Shell>();
            Container.UseInstance<IShell>(shell);
            return shell;
        }

        protected override void InitializeShell()
        {
            base.InitializeShell();
            LogAppInfo();

            // get config
            var config = Container.Resolve<IAppConfiguration>();
            ConfigurationProvider.AppConfiguration = config;

            // get process service
            ProcessServiceProvider.ProcessService = Container.Resolve<IRTSSService>();

            // get Shell to set the hardware acceleration
            var shell = Container.Resolve<IShell>();
            shell.IsGpuAccelerationActive = config.IsGpuAccelerationActive;

            Application.Current.MainWindow = (Window)Shell;

            // get last tracked WindowState
            var startupWindowState = Application.Current.MainWindow.WindowState;

            // initial startup with minimized window
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
            Application.Current.MainWindow.Show();

            // set window to tray or revert back to last tracked WindowState
            if (config.StartMinimized)
                if(config.MinimizeToTray)
                    Application.Current.MainWindow.Hide();
                else
                    Application.Current.MainWindow.WindowState = WindowState.Minimized;
            else
                    Application.Current.MainWindow.WindowState = startupWindowState;

            LogWindowState();
        }

        protected override void ConfigureContainer()
        {
            base.ConfigureContainer();

            // Intialize static classes
            PoweneticsChannelExtensions.Initialize();

			// Vertical components
			Container.ConfigureSerilogILogger(Log.Logger);
            Container.Register<IEventAggregator, EventAggregator>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "EventAggregator");
            Container.Register<ISettingsStorage, JsonSettingsStorage>(Reuse.Singleton);
            Container.Register<IAppConfiguration, CapFrameXConfiguration>(Reuse.Singleton);
            Container.RegisterInstance<IFrametimeStatisticProviderOptions>(Container.Resolve<IAppConfiguration>());
            Container.RegisterInstance(new ApplicationState(), Reuse.Singleton);

            // Prism
            Container.Register<IRegionManager, RegionManager>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "RegionManager");

            // Core components
            Container.Register<IRecordDirectoryObserver, RecordDirectoryObserver>(Reuse.Singleton);
            Container.Register<IStatisticProvider, FrametimeStatisticProvider>(Reuse.Singleton);
            Container.Register<IFrametimeAnalyzer, FrametimeAnalyzer>(Reuse.Singleton);
            Container.Register<ICaptureService, PresentMonCaptureService>(Reuse.Singleton);
            Container.Register<IRTSSService, RTSSService>(Reuse.Singleton);
            Container.Register<IOverlayEntryCore, OverlayEntryCore>(Reuse.Singleton);
            Container.Register<IOverlayService, OverlayService>(Reuse.Singleton);
            Container.Register<IOnlineMetricService, OnlineMetricService>(Reuse.Singleton);
            Container.Register<ISensorService, SensorService>(Reuse.Singleton);
            var sensorConfigFolder = Path.Combine(Environment
                .GetFolderPath(Environment.SpecialFolder.ApplicationData), @"CapFrameX\Configuration\");
            // We don't use a sensor config for new LibreHardwareMonitor based sensor service
            Container.RegisterInstance<ISensorConfig>(new SensorConfig(sensorConfigFolder), Reuse.Singleton);
            Container.Register<ISensorEntryProvider, SensorEntryProvider>(Reuse.Singleton);
            Container.Register<IOverlayEntryProvider, OverlayEntryProvider>(Reuse.Singleton);
            Container.Register<IRecordManager, RecordManager>(Reuse.Singleton);
            Container.Register<ISystemInfo, SystemInfo.NetStandard.SystemInfo>(Reuse.Singleton);
            Container.Register<IAppVersionProvider, AppVersionProvider>(Reuse.Singleton);
            Container.RegisterInstance<IWebVersionProvider>(new WebVersionProvider(), Reuse.Singleton);
            Container.Register<IUpdateCheck, UpdateCheck>(Reuse.Singleton);
            Container.Register<ILogEntryManager, LogEntryManager>(Reuse.Singleton);
            Container.Register<LoginManager>(Reuse.Singleton);
            Container.Register<ICloudManager, CloudManager>(Reuse.Singleton);
            var loggerFactory = Container.Resolve<ILoggerFactory>();
            var loggerProcessList = loggerFactory.CreateLogger<ProcessList>();
            Container.RegisterInstance(ProcessList.Create(
                filename: "Processes.json",
                foldername: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"CapFrameX\Configuration"),
                appConfiguration: Container.Resolve<IAppConfiguration>(),
                logger: loggerProcessList));
            Container.Register<SoundManager>(Reuse.Singleton);
            Container.Resolve<IEventAggregator>().GetEvent<PubSubEvent<AppMessages.OpenLoginWindow>>()
                .Subscribe(async _ =>
                {
                    var loginManager = Container.Resolve<LoginManager>();
                    await loginManager.HandleRedirect(url => Task.FromResult(Process.Start(url)));
                });
            Container.Register<CaptureManager>(Reuse.Singleton);
            Container.Register<IPoweneticsService, PoweneticsService>(Reuse.Singleton);
            Container.Register<IPoweneticsDriver, PoweneticsUSBDriver>(Reuse.Singleton);
            Container.Register<IBenchlabService, BenchlabService>(Reuse.Singleton);
            Container.Register<IThreadAffinityController, ThreadAffinityController>(Reuse.Singleton);
		}

        /// <summary>
        /// https://www.c-sharpcorner.com/forums/using-ioc-with-wpf-prism-in-mvvm
        /// </summary>
        protected override void ConfigureViewModelLocator()
        {
            base.ConfigureViewModelLocator();

            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(viewType =>
            {
                var viewName = viewType.FullName;

                // Naming convention
                viewName = viewName.Replace(".View.", ".ViewModel.");
                viewName = viewName.Replace(".Views.", ".ViewModels.");
                var viewAssemblyName = viewType.GetTypeInfo().Assembly.FullName;

                // Saving ViewModels in another assembly.
                viewAssemblyName = viewAssemblyName.Replace("View", "ViewModel");
                var suffix = viewName.EndsWith("View", StringComparison.Ordinal) ? "Model" : "ViewModel";
                var viewModelName = string.Format(CultureInfo.InvariantCulture, "{0}{1}, {2}", viewName, suffix, viewAssemblyName);
                var t = Type.GetType(viewModelName);
                return t;
            });

            ViewModelLocationProvider.SetDefaultViewModelFactory(type => Container.Resolve(type, IfUnresolved.Throw));
        }

        protected override void ConfigureModuleCatalog()
        {
            base.ConfigureModuleCatalog();

            ModuleCatalog moduleCatalog = (ModuleCatalog)ModuleCatalog;
            moduleCatalog.AddModule(typeof(CapFrameXViewRegion));
        }

        private void LogAppInfo()
        {
            var loggerFactory = Container.Resolve<ILoggerFactory>();
            var version = Container.Resolve<IAppVersionProvider>().GetAppVersion().ToString();
            var atomicTime = AtomicTime.Now.TimeOfDay;
            loggerFactory.CreateLogger<ILogger<Bootstrapper>>().LogInformation("CapFrameX {version} started at UTC {atomicTime}", version, atomicTime);
        }

        private void LogWindowState()
        {
            var loggerFactory = Container.Resolve<ILoggerFactory>();

            double height = Application.Current.MainWindow.Height;
            double width = Application.Current.MainWindow.Width;
            double positionLeft = Application.Current.MainWindow.Left;
            double positionTop = Application.Current.MainWindow.Top;

            loggerFactory.CreateLogger<ILogger<Bootstrapper>>().LogInformation("Window dimensions are {width} x {height}. Window position is {positionLeft} x {positionTop}", width, height, positionLeft, positionTop);
        }
    }
}
