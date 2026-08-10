using Serilog;
using System;
using System.Threading;
using System.Windows.Threading;

namespace CapFrameX
{
    /// <summary>
    /// Hosts the startup splash screen on its own STA thread with a private dispatcher,
    /// so its animations stay smooth while the UI thread builds the shell. All members
    /// are no-ops while the splash is not showing, and the thread is a background
    /// thread, so it can never keep the process alive.
    /// </summary>
    internal static class SplashScreenHost
    {
        private static readonly object _sync = new object();
        private static Dispatcher _dispatcher;
        private static SplashScreenWindow _window;

        public static void Show()
        {
            lock (_sync)
            {
                if (_dispatcher != null)
                    return;

                // Deliberately not disposed: the splash thread may still call Set()
                // after a timeout, and the handle is created once per process.
                var windowShown = new ManualResetEventSlim();

                var thread = new Thread(() =>
                {
                    try
                    {
                        var window = new SplashScreenWindow();
                        window.Show();

                        _window = window;
                        _dispatcher = Dispatcher.CurrentDispatcher;
                        windowShown.Set();

                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Splash screen thread failed.");
                        windowShown.Set();
                    }
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Name = "CapFrameX Splash Screen";
                thread.Start();

                windowShown.Wait(TimeSpan.FromSeconds(3));
            }
        }

        public static void SetStatus(string status)
        {
            var dispatcher = _dispatcher;
            var window = _window;
            if (dispatcher == null || window == null)
                return;

            dispatcher.BeginInvoke(new Action(() => window.SetStatus(status)));
        }

        public static void Close()
        {
            Dispatcher dispatcher;
            SplashScreenWindow window;

            lock (_sync)
            {
                dispatcher = _dispatcher;
                window = _window;
                _dispatcher = null;
                _window = null;
            }

            if (dispatcher == null)
                return;

            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (window != null)
                        await window.FadeOutAsync();
                }
                finally
                {
                    window?.Close();
                    dispatcher.InvokeShutdown();
                }
            }));
        }
    }
}
