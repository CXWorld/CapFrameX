using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.MVVM;
using CapFrameX.Contracts.Overlay;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.MVVM;
using CapFrameX.MVVM.Dialogs;
using CapFrameX.View.UITracker;
using CapFrameX.ViewModel;
using MaterialDesignThemes.Wpf;
using Prism.Events;
using Serilog;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;
        private const uint MF_GRAYED = 0x00000001;
        private const uint MF_ENABLED = 0x00000000;
        private const string ShellDialogHostIdentifier = "ShellDialogHost";

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
        private bool _isTaskbarIconRefreshed = false;

        private readonly ISettingsStorage _settingsStorage;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IPathService _pathService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IOverlayProfileChangeTracker _overlayProfileChangeTracker;
        private readonly Lazy<IOverlayEntryProvider> _overlayEntryProvider;

        private bool? _lastPublishedContentVisibility;

        private GridLength ColumnAWidthSaved { get; set; }

        public Shell(ISettingsStorage settingsStorage, IAppConfiguration appConfiguration,
            IPathService pathService,
            UpdateViewModel updateViewModel, IEventAggregator eventAggregator,
            IOverlayProfileChangeTracker overlayProfileChangeTracker,
            Lazy<IOverlayEntryProvider> overlayEntryProvider)
        {
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _overlayProfileChangeTracker = overlayProfileChangeTracker;
            _overlayEntryProvider = overlayEntryProvider;
            using (StartupPerformanceLogger.Measure("Shell XAML and resource initialization"))
            {
                InitializeComponent();
            }

            _settingsStorage = settingsStorage;
            _pathService = pathService;
            Closing += Shell_Closing;

            // Only the DialogHost gets the update view model; the regions bring their own view
            // models along, so nothing else in the shell is affected by this DataContext.
            UpdateDialogHost.DataContext = updateViewModel;

            if (PortableModeDetector.IsPortableMode)
            {
                Title = "CapFrameX Portable";
            }

            using (StartupPerformanceLogger.Measure("Shell window state tracker initialization"))
            {
                // Start tracking the Window instance.
                var windowStateTracker = new WindowStateTracker(_pathService.ConfigFolder);
                windowStateTracker.Tracker.Track(this);
                StateChanged += Resize;

                // Both hooks are needed: plain minimize only changes WindowState,
                // minimize to tray additionally calls Hide() (IsVisible).
                StateChanged += (s, e) => PublishContentVisibility();
                IsVisibleChanged += (s, e) => PublishContentVisibility();

                // Start tracking column width
                var columnAWidthTracker = new ColumnWidthTracker(this, _pathService.ConfigFolder);
                var columnBWidthTracker = new ColumnWidthTracker(this, _pathService.ConfigFolder);

                columnAWidthTracker.Tracker.Track(LeftColumn);
                columnBWidthTracker.Tracker.Track(RightColumn);
            }
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

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_isTaskbarIconRefreshed)
                return;

            _isTaskbarIconRefreshed = true;
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => IconHelper.RefreshTaskbarIcon(this)));
        }

        private void Resize(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && (ConfigurationProvider.AppConfiguration?.MinimizeToTray ?? true))
            {
                Hide();
            }
        }

        /// <summary>
        /// Lets view models pause work whose output nobody can see (e.g. the info tab's
        /// live telemetry). Visibility is the criterion, not focus: CapFrameX running on
        /// a second monitor with a game focused on the first display must keep updating.
        /// </summary>
        private void PublishContentVisibility()
        {
            bool isContentVisible = IsVisible && WindowState != WindowState.Minimized;
            if (_lastPublishedContentVisibility == isContentVisible)
                return;

            _lastPublishedContentVisibility = isContentVisible;
            _eventAggregator.GetEvent<PubSubEvent<AppMessages.ShellVisibilityChanged>>()
                .Publish(new AppMessages.ShellVisibilityChanged(isContentVisible));
        }

        private void SystemTray_TrayLeftMouseDownClick(object sender, RoutedEventArgs e)
        {
            bool minimizeToTray = ConfigurationProvider.AppConfiguration?.MinimizeToTray ?? true;

            if (minimizeToTray)
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
            else
            {
                WindowState = WindowState == WindowState.Minimized
                    ? WindowState.Normal
                    : WindowState.Minimized;

                if (WindowState == WindowState.Normal)
                    this.ShowAndFocus();
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
            // Drag-resizing keeps the column's MinWidth floor (so the control area
            // can't be squeezed below its default width), but the double-click
            // hide/show toggle deliberately bypasses that floor to fully collapse it.
            if (LeftColumn.ActualWidth > 8)
            {
                ColumnAWidthSaved = LeftColumn.Width;
                LeftColumn.MinWidth = 8;
                LeftColumn.Width = new GridLength(8, GridUnitType.Pixel);
            }
            else
            {
                LeftColumn.Width = ColumnAWidthSaved;
                LeftColumn.MinWidth = 400;
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

            bool saveOverlayProfile = false;
            bool closeWasDeferred = false;
            if (_overlayProfileChangeTracker.HasPendingChanges)
            {
                BeginDeferredClose(e);
                closeWasDeferred = true;

                if (!IsVisible)
                    Show();
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                this.ShowAndFocus();

                UnsavedOverlayProfileDialogResult result;
                try
                {
                    result = await ShowUnsavedOverlayProfileDialogAsync();
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Error while showing the unsaved overlay profile dialog.");
                    AbortDeferredClose();
                    return;
                }

                if (result == UnsavedOverlayProfileDialogResult.Cancel)
                {
                    AbortDeferredClose();
                    return;
                }

                saveOverlayProfile = result == UnsavedOverlayProfileDialogResult.SaveAndExit;
            }

            Task pendingSave = Task.CompletedTask;
            if (_settingsStorage is JsonSettingsStorage jsonStorage)
            {
                pendingSave = jsonStorage.WaitForPendingSaveAsync();
            }

            if (!saveOverlayProfile && pendingSave.IsCompleted)
            {
                if (closeWasDeferred)
                    CompleteDeferredClose();

                return;
            }

            if (!closeWasDeferred)
                BeginDeferredClose(e);

            if (saveOverlayProfile)
            {
                try
                {
                    await _overlayEntryProvider.Value.SaveOverlayEntriesToJson(
                        _appConfiguration.OverlayEntryConfigurationFile);
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex, "Error while saving the overlay profile during shutdown.");
                }

                if (_overlayProfileChangeTracker.HasPendingChanges)
                {
                    try
                    {
                        await ShowOverlayProfileSaveErrorDialogAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Error(ex, "Error while showing the overlay profile save failure dialog.");
                    }

                    AbortDeferredClose();
                    return;
                }
            }

            try
            {
                await pendingSave;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while waiting for settings to save.");
            }

            CompleteDeferredClose();
        }

        private static async Task<UnsavedOverlayProfileDialogResult> ShowUnsavedOverlayProfileDialogAsync()
        {
            object result = await DialogHost.Show(
                new UnsavedOverlayProfileDialog(), ShellDialogHostIdentifier);

            return result is UnsavedOverlayProfileDialogResult dialogResult
                ? dialogResult
                : UnsavedOverlayProfileDialogResult.Cancel;
        }

        private static async Task ShowOverlayProfileSaveErrorDialogAsync()
        {
            var dialog = new MessageDialog
            {
                DataContext = new
                {
                    MessageText = "The overlay profile could not be saved. CapFrameX will remain " +
                        "open so your changes are not lost."
                }
            };

            await DialogHost.Show(dialog, ShellDialogHostIdentifier);
        }

        private void BeginDeferredClose(CancelEventArgs e)
        {
            e.Cancel = true;
            _isShuttingDown = true;
            SetCloseButtonEnabled(false);
        }

        private void AbortDeferredClose()
        {
            _isShuttingDown = false;
            SetCloseButtonEnabled(true);
        }

        private void CompleteDeferredClose()
        {
            _isReadyToClose = true;
            _isShuttingDown = false;
            SetCloseButtonEnabled(true);

            Close();  // Retry closing after asynchronous saves complete
        }
    }
}
