using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace CapFrameX.CaptureHelper
{
    public partial class App : Application
    {
        private Mutex _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => Log.Write("Unhandled exception: " + args.ExceptionObject);

            // A second instance would take over the PresentMon session of the first one.
            _instanceMutex = new Mutex(true, @"Global\CapFrameX.CaptureHelper", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("CapFrameX Capture Helper is already running.", AppInfo.Name,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            Log.Write($"{AppInfo.Title} started on {Environment.OSVersion} " +
                $"({(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit), .NET {Environment.Version}");
            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _instanceMutex?.Dispose();
            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Write("Unhandled exception: " + e.Exception);
            MessageBox.Show(e.Exception.Message, AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }

    internal static class AppInfo
    {
        public const string Name = "CapFrameX Capture Helper";

        public static string Bitness => Environment.Is64BitProcess ? "x64" : "x86";

        public static string Title =>
            $"{Name} {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)} ({Bitness})";

        public static string PresentMonPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PresentMon", $"PresentMon-1.10.0-{Bitness}.exe");
    }
}
