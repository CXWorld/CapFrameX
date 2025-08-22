using CapFrameX.Configuration;
using CapFrameX.Contracts.MVVM;
using CapFrameX.MVVM;
using CapFrameX.View.UITracker;
using Prism;
using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;

namespace CapFrameX
{
    ///// <summary>
    ///// Interaction logic for Shell.xaml
    /// </summary>
    public partial class Shell : Window, IShell
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_ENABLED = 0x00000000;

        private void SetCloseButtonEnabled(bool enabled)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var hMenu = GetSystemMenu(hwnd, false);

            EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | (enabled ? MF_ENABLED : MF_GRAYED));
        }


        public ContentControl GlobalScreenshotArea => ScreenshotArea;

        public bool IsGpuAccelerationActive { get; set; }

        private bool _isShuttingDown = false;
        private bool _isReadyToClose = false;

        private readonly ISettingsStorage _settingsStorage;

        private GridLength ColumnAWidthSaved { get; set; }

        public Shell(ISettingsStorage settingsStorage)
        {
            InitializeComponent();
            _settingsStorage = settingsStorage;
            Closing += Shell_Closing;

            // Start tracking the Window instance.
            WindowStatServices.Tracker.Track(this);
            this.StateChanged += Resize;

            // Start tracking column width
            var columnAWidthTracker = new ColumnWidthTracker(this);
            var columnBWidthTracker = new ColumnWidthTracker(this);

            columnAWidthTracker.Tracker.Track(LeftColumn);
            columnBWidthTracker.Tracker.Track(RightColumn);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);

            if (source != null)
            {
                //source.AddHook(new HwndSourceHook(HandleMessages));
                source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
            IconHelper.RemoveIcon(this);
        }
        private void Resize(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        //private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        //{
        //    // 0x0112 == WM_SYSCOMMAND, 'Window' command message.
        //    // 0xF020 == SC_MINIMIZE, command to minimize the window.
        //    if (msg == 0x0112 && ((int)wParam & 0xFFF0) == 0xF020)
        //    {
        //        // Cancel the minimize.
        //        handled = true;
        //        Hide();
        //    }

        //    return IntPtr.Zero;
        //}

        private void SystemTray_TrayLeftMouseDownClick(object sender, RoutedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                Hide();
            }
            else
            {
                this.ShowAndFocus();
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
            }
        }

        private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
        {
            this.ShowAndFocus();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GridSplitter_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LeftColumn.ActualWidth > 8)
            {
                ColumnAWidthSaved = LeftColumn.Width;
                LeftColumn.Width = new GridLength(8, GridUnitType.Pixel);
            }
            else
            {
                LeftColumn.Width = ColumnAWidthSaved;
            }
        }

        private async void Shell_Closing(object sender, CancelEventArgs e)
        {
            if (_isReadyToClose)
            {
                // Allow the window to close normally
                return;
            }

            if (_isShuttingDown)
            {
                // Already waiting for save; prevent multiple attempts to close
                e.Cancel = true;
                return;
            }

            if (_settingsStorage is JsonSettingsStorage jsonStorage)
            {
                var pendingSave = jsonStorage.WaitForPendingSaveAsync();

                if (!pendingSave.IsCompleted)
                {
                    e.Cancel = true;
                    _isShuttingDown = true;
                    SetCloseButtonEnabled(false);

                    try
                    {
                        await pendingSave;
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error while waiting for settings to save.");
                    }

                    _isReadyToClose = true;
                    SetCloseButtonEnabled(true);

                    Close();  // Retry closing now that save is complete
                }
            }
        }
    }
}
