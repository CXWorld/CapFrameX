using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using CapFrameX.Remote;
using EmbedIO;
using Newtonsoft.Json;
using Serilog;
using Serilog.Formatting.Compact;
using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            Process currentProcess = Process.GetCurrentProcess();
            if (Process.GetProcesses().Any(p => p.ProcessName == currentProcess.ProcessName && p.Id != currentProcess.Id))
            {
                _isSingleInstance = false;
                MessageBox.Show("Already an instance running...");
                Current.Shutdown();
            }
            else
            {
                InitializeLogger();
                SetupExceptionHandling();
                base.OnStartup(e);
                _bootstrapper = new Bootstrapper();
                _bootstrapper.Run(true);

                _webServer = WebserverFactory.CreateWebServer(_bootstrapper.Container, "http://localhost:1337");
                _webServer.RunAsync();
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
                    Process.Start(
                        string.Format(ConfigurationManager.AppSettings.Get("ContactFormUriTemplate"),
                                     HttpUtility.UrlEncode($"Crashlog-Report: {reportId}"),
                                     HttpUtility.UrlEncode($@"Dear CapframeX Team,
                                        I encountered a fatal Crash.
                                        Please have a look at the Crashlog with Id {reportId}.

                                        <<< Please describe briefly what you did when the error occurred >>>

                                        Feel free to contact me by mail."),
                                     string.Empty)
                        );
                }
            }

            Current.Shutdown();
        }

        private void CapFrameXExit(object sender, ExitEventArgs e)
        {
            if (!_isSingleInstance)
                return;

            PresentMonCaptureService.TryKillPresentMon();

            var sensorService = _bootstrapper.Container.Resolve(typeof(ISensorService), true) as ISensorService;
            sensorService?.CloseOpenHardwareMonitor();

            var rtssService = _bootstrapper.Container.Resolve(typeof(IRTSSService), true) as IRTSSService;
            rtssService.ClearOSD();

            _webServer.Dispose();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
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
                var configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\Configuration\");
                var sensorConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\SensorConfiguration\");
                var overlayConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\OverlayConfiguration\");
                var processListFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\Resources\");


                if (!Directory.Exists(configFolder))
                {
                    Directory.CreateDirectory(configFolder);
                }
                else
                {
                    var oldSettingsFileName = Path.Combine(configFolder, "settings.json");
                    var newSettingsFileName = Path.Combine(configFolder, "AppSettings.json");
                    if (File.Exists(oldSettingsFileName))
                    {
                        if (!File.Exists(newSettingsFileName))
                            File.Move(oldSettingsFileName, newSettingsFileName);

                        File.Delete(oldSettingsFileName);
                    }
                }

                if (Directory.Exists(sensorConfigFolder))
                {
                    foreach (var fullPath in Directory.EnumerateFiles(sensorConfigFolder))
                    {
                        string destFile = Path.Combine(configFolder, Path.GetFileName(fullPath));
                        if (!File.Exists(destFile))
                            File.Move(fullPath, destFile);
                        else
                            File.Delete(fullPath);

                    }
                    Directory.Delete(sensorConfigFolder);
                }

                if (Directory.Exists(overlayConfigFolder))
                {
                    foreach (var fullPath in Directory.EnumerateFiles(overlayConfigFolder))
                    {
                        string destFile = Path.Combine(configFolder, Path.GetFileName(fullPath));
                        if (!File.Exists(destFile))
                            File.Move(fullPath, destFile);
                        else
                            File.Delete(fullPath);
                    }
                    Directory.Delete(overlayConfigFolder);
                }

                if (Directory.Exists(processListFolder))
                {
                    foreach (var fullPath in Directory.EnumerateFiles(processListFolder))
                    {
                        string destFile = Path.Combine(configFolder, Path.GetFileName(fullPath));
                        if (!File.Exists(destFile))
                            File.Move(fullPath, destFile);
                        else
                            File.Delete(fullPath);

                    }
                    Directory.Delete(processListFolder);
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

        public static bool IsAdministrator =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);

        private static void InitializeLogger()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"CapFrameX\Logs");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .AuditTo.Sink<InMemorySink>()
                .WriteTo.File(
                    path: Path.Combine(path, "CapFrameX.log"),
                    fileSizeLimitBytes: 1024 * 10000, // approx 10MB
                    rollOnFileSizeLimit: true, // if filesize is reached, it created a new file
                    retainedFileCountLimit: 10, // it keeps max 10 files
                    formatter: new CompactJsonFormatter()
                ).CreateLogger();
        }

    }
}
