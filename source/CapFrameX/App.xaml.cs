using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.PresentMonInterface;
using CapFrameX.Remote;
using DryIoc;
using EmbedIO;
using Newtonsoft.Json;
using Serilog;
using Serilog.Formatting.Compact;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace CapFrameX
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Bootstrapper _bootstrapper;
        private WebServer _webServer;
        private bool _isSingleInstance = true;
        private Mutex _mutex;
#if DEBUG
        private DebugMonitorWindow _debugMonitorWindow;
#endif

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "CapFrameX";
            _mutex = new Mutex(true, appName, out bool createdNew);
            if (!createdNew)
            {
                Process current = Process.GetCurrentProcess();

                foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                {
                    if (process.Id != current.Id)
                    {
                        try
                        {
                            AppHelper.ShowWindowInCorrectState(process);
                        }
                        catch(Exception ex)
                        {
                            InitializeLogger();
                            Log.Logger.Fatal(ex, "Exception in ShowWindowInCorrectState");
                        }
                        break;
                    }
                }

                _isSingleInstance = false;
                Current.Shutdown();
            }
            else
            {
                InitializeLogger();
                SetupExceptionHandling();
                base.OnStartup(e);
                _bootstrapper = new Bootstrapper();
                _bootstrapper.Run(true);

                Task.Run(async () =>
                {
                    try
                    {
                        _webServer = WebserverFactory.CreateWebServer(_bootstrapper.Container, "http://*", false);
                        await _webServer.RunAsync().ConfigureAwait(false);
                    }
                    catch (System.Net.HttpListenerException)
                    {
                        _webServer?.Dispose();
                        _webServer = WebserverFactory.CreateWebServer(_bootstrapper.Container, "http://*", true);
                        await _webServer.RunAsync().ConfigureAwait(false);
                    }
                });

#if DEBUG
                // Open debug monitor window in Debug mode
                var captureService = _bootstrapper.Container.Resolve<ICaptureService>();
                _debugMonitorWindow = new DebugMonitorWindow(captureService);
                _debugMonitorWindow.Show();
#endif
            }
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogUnhandledException((Exception)e.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException");
                ShowCrashLogUploaderMessagebox();
            };
            DispatcherUnhandledException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "Application.Current.DispatcherUnhandledException");
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved();
            };
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                message = string.Format("Unhandled exception in {0} v{1}", assemblyName.Name, assemblyName.Version);
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal(ex, "Exception in LogUnhandledException");
            }
            finally
            {
                Log.Logger.Fatal(exception, message);
            }
        }

        private void ShowCrashLogUploaderMessagebox()
        {
            if (MessageBox.Show("An unexpected Error occured. Do you want to upload the CapFrameX Log for further analysis?", "Fatal Error", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var client = new HttpClient()
                {
                    BaseAddress = new Uri(ConfigurationManager.AppSettings["WebserviceUri"])
                })
                {
                    var loggerEvents = InMemorySink.LogEvents;
                    client.DefaultRequestHeaders.AddCXClientUserAgent();

                    var content = new StringContent(JsonConvert.SerializeObject(loggerEvents));
                    content.Headers.ContentType.MediaType = "application/json";

                    var response = client.PostAsync("crashlogs", content).GetAwaiter().GetResult();

                    var reportId = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    Log.Logger.Information("Uploading Logs. Report-ID is {reportId}", reportId);
                    MessageBox.Show($"Your Report-ID is {reportId}.\nPlease include this Id in your support inquiry. Visit https://www.capframex.com/support for further information.");
                    Process.Start(string.Format(ConfigurationManager.AppSettings.Get("ContactFormUriTemplate"),
                        HttpUtility.UrlEncode($"Crashlog-Report: {reportId}"),
                        HttpUtility.UrlEncode($@"Dear CapframeX Team,
                        I encountered a fatal Crash.
                        Please have a look at the Crashlog with Id {reportId}.

                        <<< Please describe briefly what you did when the error occurred >>>

                        Feel free to contact me by mail."),
                        string.Empty));
                }
            }

            Current.Shutdown();
        }

        private void ApplicationExit(object sender, ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.Dispose();
            }

            if (!_isSingleInstance)
                return;

#if DEBUG
            try
            {
                _debugMonitorWindow?.Close();
            }
            catch { }
#endif

            try
            {
                PresentMonCaptureService.TryKillPresentMon();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while shutting down PresentMon.");
            }

            try
            {
                var sensorService = _bootstrapper.Container.Resolve<ISensorService>();
                sensorService?.ShutdownSensorService();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while shutting down the sensor service.");
            }

            try
            {
                var overlayService = _bootstrapper.Container.Resolve<IOverlayService>();
                overlayService?.ShutdownOverlayService();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while shutting down the overlay service.");
            }

            try
            {
                var rtssService = _bootstrapper.Container.Resolve<IRTSSService>();
                rtssService?.ClearOSD();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while shutting down the RTSS service.");
            }

            try
            {
                var pmdDriver = _bootstrapper.Container.Resolve<IPoweneticsDriver>();
                pmdDriver?.Disconnect();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while shutting down the PMD driver.");
            }

            try
            {
                var benchlabSerivce = _bootstrapper.Container.Resolve<IBenchlabService>();
                benchlabSerivce?.ShutDownService();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while stopping the BENCHLAB service.");
            }

            try
            {
                _webServer?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while shutting down the web server.");
            }
        }

        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            if (!IsAdministrator)
            {
                MessageBox.Show("Run CapFrameX as administrator. Right click on desktop shortcut" + Environment.NewLine
                    + "and got to Properties -> Shortcut -> Advanced then check option Run as administrator.");
                Current.Shutdown();
            }

            // unify old config folders
            try
            {
                var configFolderV1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\Configuration\");
                var configFolderV2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"CapFrameX\Configuration\");

                if (!Directory.Exists(configFolderV2))
                {
                    Directory.CreateDirectory(configFolderV2);
                }

                if (Directory.Exists(configFolderV1))
                {
                    var oldSettingsFileName = Path.Combine(configFolderV1, "settings.json");
                    var newSettingsFileName = Path.Combine(configFolderV1, "AppSettings.json");
                    if (File.Exists(oldSettingsFileName))
                    {
                        if (!File.Exists(newSettingsFileName))
                            File.Move(oldSettingsFileName, newSettingsFileName);

                        File.Delete(oldSettingsFileName);
                    }

                    CleanupOldConfigStorage(configFolderV1, configFolderV2);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while creating config folder.");
            }

            // create capture folder
            try
            {
                var captureFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    @"CapFrameX\Captures\");

                if (!Directory.Exists(captureFolder))
                {
                    Directory.CreateDirectory(captureFolder);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while creating capture folder.");
            }
        }

        private void CleanupOldConfigStorage(string configFolderV1, string configFolderV2)
        {
            try
            {
                var oldAppSettingsFileName = Path.Combine(configFolderV1, "AppSettings.json");
                var oldOverlayEntryConfiguration_0FileName = Path.Combine(configFolderV1, "OverlayEntryConfiguration_0.json");
                var oldOverlayEntryConfiguration_1FileName = Path.Combine(configFolderV1, "OverlayEntryConfiguration_1.json");
                var oldOverlayEntryConfiguration_2FileName = Path.Combine(configFolderV1, "OverlayEntryConfiguration_2.json");
                var oldProcessesFileName = Path.Combine(configFolderV1, "Processes.json");
                var oldSensorEntryConfigurationFileName = Path.Combine(configFolderV1, "SensorEntryConfiguration.json");

                var newAppSettingsFileName = Path.Combine(configFolderV2, "AppSettings.json");
                var newOverlayEntryConfiguration_0FileName = Path.Combine(configFolderV2, "OverlayEntryConfiguration_0.json");
                var newOverlayEntryConfiguration_1FileName = Path.Combine(configFolderV2, "OverlayEntryConfiguration_1.json");
                var newOverlayEntryConfiguration_2FileName = Path.Combine(configFolderV2, "OverlayEntryConfiguration_2.json");
                var newProcessesFileName = Path.Combine(configFolderV2, "Processes.json");
                var newSensorEntryConfigurationFileName = Path.Combine(configFolderV2, "SensorEntryConfiguration.json");

                if (File.Exists(oldAppSettingsFileName))
                {
                    if (!File.Exists(newAppSettingsFileName))
                        File.Move(oldAppSettingsFileName, newAppSettingsFileName);

                    File.Delete(oldAppSettingsFileName);
                }

                if (File.Exists(oldOverlayEntryConfiguration_0FileName))
                {
                    if (!File.Exists(newOverlayEntryConfiguration_0FileName))
                        File.Move(oldOverlayEntryConfiguration_0FileName, newOverlayEntryConfiguration_0FileName);

                    File.Delete(oldOverlayEntryConfiguration_0FileName);
                }

                if (File.Exists(oldOverlayEntryConfiguration_1FileName))
                {
                    if (!File.Exists(newOverlayEntryConfiguration_1FileName))
                        File.Move(oldOverlayEntryConfiguration_1FileName, newOverlayEntryConfiguration_1FileName);

                    File.Delete(oldOverlayEntryConfiguration_1FileName);
                }

                if (File.Exists(oldOverlayEntryConfiguration_2FileName))
                {
                    if (!File.Exists(newOverlayEntryConfiguration_2FileName))
                        File.Move(oldOverlayEntryConfiguration_2FileName, newOverlayEntryConfiguration_2FileName);

                    File.Delete(oldOverlayEntryConfiguration_2FileName);
                }

                if (File.Exists(oldProcessesFileName))
                {
                    if (!File.Exists(newProcessesFileName))
                        File.Move(oldProcessesFileName, newProcessesFileName);

                    File.Delete(oldProcessesFileName);
                }

                if (File.Exists(oldSensorEntryConfigurationFileName))
                {
                    if (!File.Exists(newSensorEntryConfigurationFileName))
                        File.Move(oldSensorEntryConfigurationFileName, newSensorEntryConfigurationFileName);

                    File.Delete(oldSensorEntryConfigurationFileName);
                }

                Directory.Delete(configFolderV1);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while moving old config files.");
            }
        }

        public static bool IsAdministrator =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

        private static void InitializeLogger()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"CapFrameX\Logs");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .AuditTo.Sink<InMemorySink>()
                .WriteTo.File(
                    path: Path.Combine(path, "CapFrameX.log"),
                    fileSizeLimitBytes: 1024 * 10000, // approx 10MB
                    rollOnFileSizeLimit: true, // if filesize is reached, it created a new file
                    retainedFileCountLimit: 10, // it keeps max 10 files
                    formatter: new CompactJsonFormatter()).CreateLogger();
        }

    }
}
