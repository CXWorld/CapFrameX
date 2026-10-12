using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;

namespace CapFrameX.View
{
    /// <summary>Independent host until the designer is embedded in the Overlay tab.</summary>
    public partial class OverlayDesignerWindow : Window
    {
        private bool _closeApproved;
        private bool _closePending;
        private bool _ownerClosing;

        public OverlayDesignerWindow(IOverlayTelemetryService telemetry,
            IObservable<OverlayPreviewFrame> frameFeed = null, string configurationFolder = null,
            IOverlayDesignService designService = null, OverlayClassicPreviewFeed classicFeed = null,
            Action editClassicProfile = null)
        {
            InitializeComponent();
            // Keep the larger workspace within the current desktop on smaller displays.
            Width = Math.Min(Width, SystemParameters.WorkArea.Width);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height);
            Designer.ConfigureDesignService(designService);
            Designer.TelemetryService = telemetry;
            Designer.FrameFeed = frameFeed;
            Designer.ConfigureClassicPreview(classicFeed, editClassicProfile);
            if (configurationFolder != null)
            {
                Designer.ConfigureProfiles(configurationFolder);
            }
            Closing += OnClosing;
        }

        public bool RequiresCloseConfirmation => Designer.HasUnsavedChanges || Designer.IsProfileOperationPending;

        /// <summary>Reuses the current workspace and its unsaved-change guard for a library request.</summary>
        public async Task<bool> OpenProfileAsync(string profileId = null)
        {
            if (_closePending || _closeApproved || _ownerClosing)
                return false;
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            if (!IsVisible)
                Show();
            Activate();
            return string.IsNullOrWhiteSpace(profileId) || await Designer.OpenProfileAsync(profileId);
        }

        public void SetOwnerClosing(bool closing)
        {
            _ownerClosing = closing;
            Designer.SetHostClosing(closing || _closePending || _closeApproved);
        }

        public async Task<bool> PrepareCloseAsync()
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            if (!IsVisible)
            {
                Show();
            }
            Activate();
            return await Designer.TryCloseAsync();
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (_closeApproved)
            {
                return;
            }
            e.Cancel = true;
            if (_closePending)
            {
                return;
            }
            _closePending = true;
            Designer.SetHostClosing(true);
            try
            {
                if (await PrepareCloseAsync())
                {
                    _closeApproved = true;
                    _ = Dispatcher.BeginInvoke(new Action(Close));
                }
            }
            finally
            {
                _closePending = false;
                // Keep input frozen across the dispatcher turn before the approved Close.
                Designer.SetHostClosing(_ownerClosing || _closeApproved);
            }
        }
    }
}
