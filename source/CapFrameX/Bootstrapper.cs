using CapFrameX.Capture.Contracts;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.MVVM;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Contracts.Update;
using CapFrameX.Data;
using CapFrameX.Data.Logging;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Hardware.Controller;
using CapFrameX.Hotkey;
using CapFrameX.Mcp.Tools;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.PresentMonInterface;
using CapFrameX.PresentMonInterface.AmdFlm;
using CapFrameX.RTSSIntegration;
using CapFrameX.Sensor;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Updater;
using CapFrameX.ViewModel;
using DryIoc;
using Microsoft.Extensions.Logging;
using Prism.Container.DryIoc;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Navigation.Regions;
using Serilog;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX
{
    public partial class App
    {
        // Keeps the hook-free OSD bridge alive for the app lifetime.
        private OSD.Integration.OsdOverlayBridge _osdOverlayBridge;
        // Manages in-game hook injection into the detected game process.
        private OSD.Integration.HookOverlayManager _hookOverlayManager;
        // Publishes the hook's native handshake/heartbeat to view models.
        private OSD.Integration.HookOverlayStatusService _hookOverlayStatusService;
        // Publishes CapFrameX's overlay entries to the in-game hook via shared memory.
        private OSD.Integration.HookMetricsPublisher _hookMetricsPublisher;
        // Streams per-frame PresentMon frametimes/display-times to the hook (PresentMon graph mode).
        private OSD.Integration.HookFrametimePublisher _hookFrametimePublisher;

        // The existing composition root uses DryIoc-specific registration APIs. PrismApplication
        // exposes the container through Prism's abstraction, so unwrap it in one place.
        private new IContainer Container => ((DryIocContainerExtension)base.Container).Instance;

        /// <summary>
        /// While the splash screen is up, the shell stays minimized after its warm-up
        /// Show(); App reveals it via <see cref="PendingMainWindowState"/> as soon as the
        /// Prism initialization is through, so the splash covers the shell construction and nothing
        /// more.
        /// </summary>
        public bool DeferMainWindowRestore { get; set; }

        public WindowState? PendingMainWindowState { get; private set; }

        protected override Window CreateShell()
        {
            using (StartupPerformanceLogger.Measure("Shell resolution and construction"))
            {
                var shell = Container.Resolve<Shell>();
                Container.RegisterInstance<IShell>(shell);
                return shell;
            }
        }

        protected override void InitializeShell(Window shell)
        {
            using (StartupPerformanceLogger.Measure("App.InitializeShell total"))
            {
                using (StartupPerformanceLogger.Measure("Prism base shell initialization"))
                {
                    base.InitializeShell(shell);
                }

                using (StartupPerformanceLogger.Measure("Application metadata logging"))
                {
                    LogAppInfo();
                }

                IAppConfiguration config;
                using (StartupPerformanceLogger.Measure("Application configuration resolution"))
                {
                    config = Container.Resolve<IAppConfiguration>();
                    ConfigurationProvider.AppConfiguration = config;
                }

                using (StartupPerformanceLogger.Measure("BENCHLAB service demand-start configuration"))
                {
                    try
                    {
                        var benchlabService = Container.Resolve<IBenchlabService>();
                        if (!benchlabService.EnsureDemandStartMode())
                        {
                            Log.Logger.Warning("Could not configure the BENCHLAB service for demand start.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error configuring the BENCHLAB service for demand start.");
                    }
                }

                using (StartupPerformanceLogger.Measure("Path service resolution"))
                {
                    var pathService = Container.Resolve<IPathService>();
                    PathServiceProvider.PathService = pathService;
                }

                IRTSSService rtssService;
                using (StartupPerformanceLogger.Measure("RTSS service dependency graph initialization"))
                {
                    rtssService = Container.Resolve<IRTSSService>();
                    ProcessServiceProvider.ProcessService = rtssService;
                }

                IOverlayService osdOverlayService;
                using (StartupPerformanceLogger.Measure("Overlay service dependency graph initialization"))
                {
                    osdOverlayService = Container.Resolve<CapFrameX.Contracts.Overlay.IOverlayService>();
                }

                ICaptureService osdCaptureService;
                using (StartupPerformanceLogger.Measure("Capture service dependency graph initialization"))
                {
                    osdCaptureService = Container.Resolve<ICaptureService>();
                }

                // Resolve the opt-in FLM service at startup so it can measure independently of
                // PresentMon captures and feed both the live metric and sensor pipelines.
                Container.Resolve<IAmdFlmService>();

                // Only the composition root sees both the RTSS integration and the OSD's Vulkan
                // probes, so the "is this target presenting through Vulkan?" answer is handed over
                // here. RTSS uses it to decide whether it may still be launched into a running
                // game: injecting into a live DXGI title is its normal mode, into a live Vulkan
                // title it can only do harm — the implicit layer it would need is bound at
                // vkCreateInstance and cannot be added afterwards.
                rtssService.VulkanPresentationProbe =
                    OSD.Integration.VulkanPresentation.IsActive;

                // In-game hook overlay: inject cfx_osd_hook.dll into the game CapFrameX already
                // detected. The PID flows through IRTSSService.ProcessIdStream (IProcessService),
                // the same stream the overlay/capture pipeline uses — so we address the process
                // directly, no manual injector. It also publishes the transient hook-free fallback
                // state when injection is unsafe, fails its handshake, or the runtime is unsupported.
                using (StartupPerformanceLogger.Measure("Hook overlay manager initialization"))
                {
                    _hookOverlayManager = new OSD.Integration.HookOverlayManager(
                        config,
                        rtssService.ProcessIdStream,
                        osdCaptureService.FrameDataStream,
                        PresentMonCaptureService.ProcessID_INDEX,
                        PresentMonCaptureService.PresentRuntime_INDEX,
                        statusService: _hookOverlayStatusService);
                }

                // CapFrameX.OSD: hook-free DWM/DirectComposition overlay. Scalars come from the
                // same IOverlayEntry[] stream RTSS uses; per-present frametimes from the capture
                // service. Besides the explicit hook-free mode, it takes over transiently when the
                // selected in-game renderer cannot safely become operational (including D3D9).
                using (StartupPerformanceLogger.Measure("OSD overlay bridge initialization"))
                {
                    _osdOverlayBridge = new OSD.Integration.OsdOverlayBridge(
                        osdOverlayService,
                        config, // IAppConfiguration (resolved above) — gates OSD vs RTSS
                        osdCaptureService.FrameDataStream,
                        PresentMonCaptureService.MsBetweenPresents_INDEX,
                        PresentMonCaptureService.PresentRuntime_INDEX,
                        PresentMonCaptureService.MsBetweenDisplayChange_INDEX,
                        () => osdCaptureService.CPUStartQPCTimeInMs_Index,
                        _hookOverlayManager.HookFreeFallbackStream,
                        processIdStream: rtssService.ProcessIdStream,
                        processIdColumnIndex:
                            PresentMonCaptureService.ProcessID_INDEX);
                }

                // While the in-game hook overlay is on, mirror CapFrameX's processed overlay entries
                // (fps/lows/sensors/static rows — the same set RTSS/hook-free render) into shared memory
                // so the injected hook shows authoritative values, not just its local frame ring.
                using (StartupPerformanceLogger.Measure("Hook metrics publisher initialization"))
                {
                    _hookMetricsPublisher = new OSD.Integration.HookMetricsPublisher(
                        osdOverlayService,
                        config,
                        rtssService.ProcessIdStream);
                }

                // Optional PresentMon graph source for the hook: stream the SAME per-frame frametime +
                // display-time data the hook-free bridge consumes into a shared-memory ring the hook
                // replays. Active only with EnableHookOverlay + HookOverlayUsePresentMonFrametimes; the
                // hook otherwise uses its own local present ring (the two sources stay strictly separate).
                using (StartupPerformanceLogger.Measure("Hook frametime publisher initialization"))
                {
                    _hookFrametimePublisher = new OSD.Integration.HookFrametimePublisher(
                        config,
                        osdCaptureService.FrameDataStream,
                        rtssService.ProcessIdStream,
                        PresentMonCaptureService.ProcessID_INDEX,
                        PresentMonCaptureService.MsBetweenPresents_INDEX,
                        PresentMonCaptureService.MsBetweenDisplayChange_INDEX,
                        () => osdCaptureService.CPUStartQPCTimeInMs_Index);
                }

                using (StartupPerformanceLogger.Measure("Shell configuration"))
                {
                    var shellContract = Container.Resolve<IShell>();
                    shellContract.IsGpuAccelerationActive = config.IsGpuAccelerationActive;
                    Current.MainWindow = shell;
                }

                // get last tracked WindowState
                var startupWindowState = Current.MainWindow.WindowState;

                // initial startup with minimized window
                using (StartupPerformanceLogger.Measure("Main window first show"))
                {
                    Current.MainWindow.WindowState = WindowState.Minimized;
                    Current.MainWindow.Show();
                }

                // set window to tray or revert back to last tracked WindowState
                if (config.StartMinimized)
                    if(config.MinimizeToTray)
                        Current.MainWindow.Hide();
                    else
                        Current.MainWindow.WindowState = WindowState.Minimized;
                else if (DeferMainWindowRestore)
                    // Splash screen active: hand the restore over to App, which reveals the
                    // window once this method returned.
                    PendingMainWindowState = startupWindowState;
                else
                        Current.MainWindow.WindowState = startupWindowState;

                LogWindowState();
            }
        }

        protected override void OnInitialized()
        {
            // InitializeShell already applies the persisted minimized/tray state. Prism's default
            // implementation calls Show() and would reveal a window that was deliberately hidden.
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            using (StartupPerformanceLogger.Measure("App.RegisterTypes total"))
            {
                using (StartupPerformanceLogger.Measure("Powenetics channel initialization"))
                {
                    PoweneticsChannelExtensions.Initialize();
                }

                using (StartupPerformanceLogger.Measure("Logging and event aggregator registration"))
                {
                    Container.ConfigureSerilogILogger(Log.Logger);
                    Container.Register<IEventAggregator, EventAggregator>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "EventAggregator");
                }

                IPathService pathService;
                IAppConfiguration appConfiguration;
                using (StartupPerformanceLogger.Measure("Configuration services registration and initialization"))
                {
                    // Register PathService first - other services depend on it for path resolution
                    Container.Register<IPathService, PathService>(Reuse.Singleton);
                    Container.Register<ISettingsStorage, JsonSettingsStorage>(Reuse.Singleton);
                    Container.Register<IAppConfiguration, CapFrameXConfiguration>(Reuse.Singleton);

                    using (StartupPerformanceLogger.Measure("Path service initialization"))
                    {
                        pathService = Container.Resolve<IPathService>();
                    }

                    using (StartupPerformanceLogger.Measure("Settings and application configuration loading"))
                    {
                        appConfiguration = Container.Resolve<IAppConfiguration>();
                    }

                    Container.RegisterInstance<IFrametimeStatisticProviderOptions>(appConfiguration);
                    Container.RegisterInstance(new ApplicationState());
                    _hookOverlayStatusService = new OSD.Integration.HookOverlayStatusService();
                    Container.RegisterInstance<IHookOverlayStatusService>(
                        _hookOverlayStatusService);
                }

                using (StartupPerformanceLogger.Measure("Prism and core service registrations"))
                {
                    Container.Register<IRegionManager, RegionManager>(Reuse.Singleton, null, null, IfAlreadyRegistered.Replace, "RegionManager");
                    Container.Register<IRecordDirectoryObserver, RecordDirectoryObserver>(Reuse.Singleton);
                    Container.Register<IStatisticProvider, FrametimeStatisticProvider>(Reuse.Singleton);
                    Container.Register<IFrametimeAnalyzer, FrametimeAnalyzer>(Reuse.Singleton);
                    Container.Register<ICaptureService, PresentMonCaptureService>(Reuse.Singleton);
                    Container.Register<IRTSSService, RTSSService>(Reuse.Singleton);
                    Container.Register<IOverlayEntryCore, OverlayEntryCore>(Reuse.Singleton);
                    Container.Register<IOverlayService, OverlayService>(Reuse.Singleton);
                    Container.Register<IAmdFlmService, AmdFlmService>(Reuse.Singleton);
                    Container.Register<IOnlineMetricService, OnlineMetricService>(Reuse.Singleton);
                    Container.Register<ISensorService, SensorService>(Reuse.Singleton);
                }

                using (StartupPerformanceLogger.Measure("Sensor and overlay configuration registration"))
                {
                    var sensorConfigFolder = pathService.ConfigFolder;
                    // We don't use a sensor config for new LibreHardwareMonitor based sensor service
                    Container.RegisterInstance<ISensorConfig>(new SensorConfig(sensorConfigFolder));
                    Container.Register<ISensorEntryProvider, SensorEntryProvider>(Reuse.Singleton);
                    Container.Register<IOverlayProfileChangeTracker, OverlayProfileChangeTracker>(Reuse.Singleton);
                    Container.Register<IOverlayEntryProvider, OverlayEntryProvider>(Reuse.Singleton);
                    Container.Register<IOverlayTemplateService, OverlayTemplateService>(Reuse.Singleton);
                    Container.Register<IRecordManager, RecordManager>(Reuse.Singleton);
                }

                using (StartupPerformanceLogger.Measure("MCP tool registrations"))
                {
                    // MCP tools are resolved by McpToolRegistry via reflection-based discovery.
                    Container.Register<RecordTools>(Reuse.Singleton);
                    Container.Register<MetricsTools>(Reuse.Singleton);
                    Container.Register<SensorTools>(Reuse.Singleton);
                    Container.Register<PmdTools>(Reuse.Singleton);
                    Container.Register<ComparisonTools>(Reuse.Singleton);
                    Container.Register<SearchTools>(Reuse.Singleton);
                    Container.Register<BottleneckTools>(Reuse.Singleton);
                    Container.Register<DiagnosticsTools>(Reuse.Singleton);
                    Container.Register<CaptureTimelineTools>(Reuse.Singleton);
                    Container.Register<SystemInfoTools>(Reuse.Singleton);
                    Container.Register<CaptureStatusTools>(Reuse.Singleton);
                    Container.Register<SensorConfigTools>(Reuse.Singleton);
                    Container.Register<OverlayConfigTools>(Reuse.Singleton);
                    Container.Register<OsdOptionsTools>(Reuse.Singleton);
                }

                using (StartupPerformanceLogger.Measure("Application service registrations and initialization"))
                {
                    Container.Register<ISystemInfo, SystemInfo.NetStandard.SystemInfo>(Reuse.Singleton);
                    Container.Register<IAppVersionProvider, AppVersionProvider>(Reuse.Singleton);

					// The update service needs its catalog URI and the staging folder, neither of
					// which the container can supply, so it is built here like the process list below.
					var updateServiceLogger = Container.Resolve<ILoggerFactory>().CreateLogger<UpdateService>();
					Container.RegisterInstance<IUpdateService>(new UpdateService(
						ConfigurationManager.AppSettings["UpdateCatalogUri"],
                        Container.Resolve<IAppVersionProvider>(),
                        pathService.UpdatesFolder,
                        updateServiceLogger));

                    // Status bar, options popup and the embedded dialog share one instance.
                    Container.Register<UpdateViewModel>(Reuse.Singleton);

                    Container.Register<ILogEntryManager, LogEntryManager>(Reuse.Singleton);
                    Container.Register<LoginManager>(Reuse.Singleton);
                    Container.Register<ICloudManager, CloudManager>(Reuse.Singleton);

                    using (StartupPerformanceLogger.Measure("Process list loading"))
                    {
                        var loggerFactory = Container.Resolve<ILoggerFactory>();
                        var loggerProcessList = loggerFactory.CreateLogger<ProcessList>();
                        Container.RegisterInstance(ProcessList.Create(
                            filename: "Processes.json",
                            foldername: pathService.ConfigFolder,
                            appConfiguration: appConfiguration,
                            logger: loggerProcessList));
                    }

                    Container.Register<SoundManager>(Reuse.Singleton);

                    // The global hotkey hook is static and lives outside the container. Handing it
                    // a logger here — before any view model registers a hotkey — is what makes hook
                    // installation, re-arming and triggered hotkeys visible in the app log; a hook
                    // that Windows removes leaves no other trace.
                    HotkeyDictionaryBuilder.Logger = Container.Resolve<ILoggerFactory>()
                        .CreateLogger("CapFrameX.Hotkey");

                    using (StartupPerformanceLogger.Measure("Application event subscriptions"))
                    {
                        Container.Resolve<IEventAggregator>().GetEvent<PubSubEvent<AppMessages.OpenLoginWindow>>()
                            .Subscribe(async _ =>
                            {
                                var loginManager = Container.Resolve<LoginManager>();
                                // UseShellExecute is required to open a URL in the default browser on .NET Core+
                                await loginManager.HandleRedirect(url => Task.FromResult(Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })));
                            });
                    }

                    Container.Register<CaptureManager>(Reuse.Singleton);
                    Container.Register<IPoweneticsService, PoweneticsService>(Reuse.Singleton);
                    Container.Register<IPoweneticsDriver, PoweneticsUSBDriver>(Reuse.Singleton);
                    Container.Register<IBenchlabService, BenchlabService>(Reuse.Singleton);
                    Container.Register<IThreadAffinityController, ThreadAffinityController>(Reuse.Singleton);
                }
            }
        }

        /// <summary>
        /// https://www.c-sharpcorner.com/forums/using-ioc-with-wpf-prism-in-mvvm
        /// </summary>
        protected override void ConfigureViewModelLocator()
        {
            using (StartupPerformanceLogger.Measure("ViewModel locator configuration"))
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

                // Timed separately from the view registration around it: that step covers XAML
                // parsing, the visual tree and the view model together, and only the split shows
                // which of the two a slow tab actually pays for.
                ViewModelLocationProvider.SetDefaultViewModelFactory(type =>
                {
                    using (StartupPerformanceLogger.Measure("ViewModel resolution: " + type.Name))
                    {
                        return Container.Resolve(type, IfUnresolved.Throw);
                    }
                });
            }
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            using (StartupPerformanceLogger.Measure("Module catalog configuration"))
            {
                base.ConfigureModuleCatalog(moduleCatalog);
                ((ModuleCatalog)moduleCatalog).AddModule(typeof(CapFrameXViewRegion));
            }
        }

        private void LogAppInfo()
        {
            var loggerFactory = Container.Resolve<ILoggerFactory>();
            var version = Container.Resolve<IAppVersionProvider>().GetAppVersion().ToString();
            var utcTime = DateTime.UtcNow.TimeOfDay;
            loggerFactory.CreateLogger<App>().LogInformation("CapFrameX {version} started at UTC {utcTime}", version, utcTime);
        }

        private void LogWindowState()
        {
            var loggerFactory = Container.Resolve<ILoggerFactory>();

            double height = Current.MainWindow.Height;
            double width = Current.MainWindow.Width;
            double positionLeft = Current.MainWindow.Left;
            double positionTop = Current.MainWindow.Top;

            loggerFactory.CreateLogger<App>().LogInformation("Window dimensions are {width} x {height}. Window position is {positionLeft} x {positionTop}", width, height, positionLeft, positionTop);
        }
    }
}
