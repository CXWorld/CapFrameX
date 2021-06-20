using CapFrameX.Contracts.MVVM;
using CapFrameX.MVVM;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace CapFrameX
{
    ///// <summary>
    ///// Interaction logic for Shell.xaml
    /// </summary>
    public partial class Shell : Window, IShell
    {
        public System.Windows.Controls.ContentControl GlobalScreenshotArea => ScreenshotArea;

        public bool IsGpuAccelerationActive { get; set; }

        public Shell()
        {
            InitializeComponent();

            // Start tracking the Window instance.
            WindowStatServices.Tracker.Track(this);

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
                source.AddHook(new HwndSourceHook(HandleMessages));
                source.CompositionTarget.RenderMode
                    = IsGpuAccelerationActive ? RenderMode.Default : RenderMode.SoftwareOnly;
            }

            base.OnSourceInitialized(e);
        }

        private IntPtr HandleMessages(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 0x0112 == WM_SYSCOMMAND, 'Window' command message.
            // 0xF020 == SC_MINIMIZE, command to minimize the window.
            if (msg == 0x0112 && ((int)wParam & 0xFFF0) == 0xF020)
            {
                // Cancel the minimize.
                handled = true;
                Hide();
            }

            return IntPtr.Zero;
        }

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
    }
}
