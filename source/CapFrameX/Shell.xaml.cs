using CapFrameX.Contracts.MVVM;
using CapFrameX.MVVM;
using CapFrameX.View.UITracker;
using System;
using System.Diagnostics;
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
        public ContentControl GlobalScreenshotArea => ScreenshotArea;

        public bool IsGpuAccelerationActive { get; set; }

        private GridLength ColumnAWidthSaved { get; set; }

        public Shell()
        {
            InitializeComponent();

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
    }
}
