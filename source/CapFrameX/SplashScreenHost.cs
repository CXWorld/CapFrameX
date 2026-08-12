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
    /// <para>
    /// <see cref="Show"/> does not wait for the window to come up. Standing up a second
    /// WPF dispatcher costs a few hundred milliseconds, and blocking the UI thread for it
    /// put that straight into the startup path - the whole point of the separate thread is
    /// that the two run side by side. Everything the caller can do to a splash that is not
    /// up yet is therefore recorded and applied once it is.
    /// </para>
    /// </summary>
    internal static class SplashScreenHost
    {
        private static readonly object _sync = new object();
        private static Dispatcher _dispatcher;
        private static SplashScreenWindow _window;
        private static string _pendingStatus;
        private static bool _closeRequested;
        private static bool _started;

        public static void Show()
        {
            lock (_sync)
            {
                if (_started)
                    return;

                _started = true;
            }

            var thread = new Thread(RunSplashScreen)
            {
                IsBackground = true,
                Name = "CapFrameX Splash Screen"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void RunSplashScreen()
        {
            try
            {
                var window = new SplashScreenWindow();
                string pendingStatus;

                lock (_sync)
                {
                    // Startup was already through (or aborted) before the window existed.
                    // Showing it now would leave a splash nobody closes.
                    if (_closeRequested)
                        return;

                    _window = window;
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    pendingStatus = _pendingStatus;
                }

                if (pendingStatus != null)
                    window.SetStatus(pendingStatus);

                window.Show();

                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Splash screen thread failed.");
            }
        }

        public static void SetStatus(string status)
        {
            Dispatcher dispatcher;
            SplashScreenWindow window;

            lock (_sync)
            {
                if (_dispatcher == null)
                {
                    // Not up yet - the thread picks the latest status up before showing.
                    _pendingStatus = status;
                    return;
                }

                dispatcher = _dispatcher;
                window = _window;
            }

            dispatcher.BeginInvoke(new Action(() => window.SetStatus(status)));
        }

        public static void Close()
        {
            Dispatcher dispatcher;
            SplashScreenWindow window;

            lock (_sync)
            {
                _closeRequested = true;
                dispatcher = _dispatcher;
                window = _window;
                _dispatcher = null;
                _window = null;
            }

            // No dispatcher yet: the flag set above makes the splash thread skip its
            // Show() instead, so no window is left behind.
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
