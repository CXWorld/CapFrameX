using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayDesignService : IOverlayDesignService, IDisposable
    {
        private readonly string _configurationFolder;
        private readonly IAppConfiguration _configuration;
        private readonly Action _showOverlay;
        private readonly object _gate = new object();
        private IReadOnlyList<OverlayDesignInfo> _profiles = Array.Empty<OverlayDesignInfo>();
        private OverlayRuntimeDesign _current;
        private string _status = "Row overlay";
        private bool _runtimeError;
        private string _statusBeforeRuntimeError;
        private readonly IDisposable _configurationSubscription;
        private bool _updatingConfiguration;

        public OverlayDesignService(string configurationFolder, IAppConfiguration configuration)
            : this(configurationFolder, configuration, null)
        {
        }

        public OverlayDesignService(string configurationFolder, IAppConfiguration configuration, Action showOverlay)
        {
            _configurationFolder = configurationFolder ?? throw new ArgumentNullException(nameof(configurationFolder));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _showOverlay = showOverlay ?? (() => _configuration.IsOverlayActive = true);
            RefreshProfiles();
            _configurationSubscription = configuration.OnValueChanged?
                .Where(change => change.key == nameof(IAppConfiguration.EnableHookOverlay)
                    || change.key == nameof(IAppConfiguration.EnableHookFreeOverlay)
                    || change.key == nameof(IAppConfiguration.ActiveOverlayDesignProfileId))
                .Subscribe(change => OnConfigurationChanged(change.key));
        }

        public IReadOnlyList<OverlayDesignInfo> Profiles { get { lock (_gate) return _profiles; } }
        public OverlayRuntimeDesign CurrentDesign { get { lock (_gate) return _current; } }
        public string ActiveProfileId => CurrentDesign?.ProfileId;
        public bool IsEnabled => CurrentDesign != null;
        public bool CanActivate => _configuration.EnableHookOverlay || _configuration.EnableHookFreeOverlay;
        public string Status { get { lock (_gate) return _status; } }
        public event EventHandler Changed;

        public void RefreshProfiles()
        {
            lock (_gate)
            {
                // Enforce the renderer gate before opening the library: even unreadable or newer
                // files must not keep a previous design alive after switching to RTSS.
                if (!CanActivate) ClearActiveDesign();
                try
                {
                    var store = Open();
                    _profiles = Array.AsReadOnly(store.Profiles.Select(profile => new OverlayDesignInfo(profile.Id, profile.Name)).ToArray());
                    string selected = _configuration.ActiveOverlayDesignProfileId;
                    var active = store.Profiles.FirstOrDefault(profile => profile.Id == selected);
                    // Recovery can return an older backup from before the current profile
                    // existed. This is not an intentional deletion: retain a scene that was
                    // already validated in this process while the user repairs the library.
                    bool retainValidated = active == null && store.RecoveryWarning != null
                        && _current != null && _current.ProfileId == selected;
                    if (!retainValidated)
                        _current = active == null ? null : OverlayRuntimeDesign.Parse(active.Id, active.Name, active.DesignJson);
                    if (active == null && !string.IsNullOrEmpty(selected) && !retainValidated)
                    {
                        SetActiveProfileId(null);
                        _status = "The active tile design was deleted. Row overlay restored.";
                    }
                    else
                    {
                        _status = CurrentModeStatus();
                    }
                    if (store.RecoveryWarning != null) _status += " · " + store.RecoveryWarning;
                    if (store.StarterInitializationWarning != null) _status += " · " + store.StarterInitializationWarning;
                    _runtimeError = false;
                    _statusBeforeRuntimeError = null;
                }
                catch (Exception error)
                {
                    // Preserve the last fully validated design and the files. On initial load
                    // there is no such snapshot, so the classic overlay stays active.
                    _status = "Saved designs could not be loaded; " + (_current == null ? CurrentModeStatus() + ". "
                        : "the last loaded design remains active. ") + error.Message;
                    _runtimeError = false;
                    _statusBeforeRuntimeError = null;
                    Log.Warning(error, "Overlay designer: runtime library refresh failed");
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Activate(string profileId)
        {
            lock (_gate)
            {
                if (!CanActivate)
                    throw new InvalidOperationException("RTSS uses Row overlay. Select In-game or Hook-free before activating a tile design.");
                var store = Open();
                var profile = store.Get(profileId);
                var design = OverlayRuntimeDesign.Parse(profile.Id, profile.Name, profile.DesignJson);
                SetActiveProfileId(profile.Id);
                _current = design;
                _profiles = Array.AsReadOnly(store.Profiles.Select(item => new OverlayDesignInfo(item.Id, item.Name)).ToArray());
                _status = CurrentModeStatus();
                if (store.RecoveryWarning != null) _status += " · " + store.RecoveryWarning;
                if (store.StarterInitializationWarning != null) _status += " · " + store.StarterInitializationWarning;
                _runtimeError = false;
                _statusBeforeRuntimeError = null;
            }
            // Explicitly using a saved design also shows the overlay. Publish this setting
            // outside the service lock: its synchronous listeners acquire renderer locks.
            // Merely browsing, refreshing, or restoring a selection never changes visibility.
            _showOverlay();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Deactivate()
        {
            lock (_gate)
            {
                ClearActiveDesign();
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Delete(string profileId)
        {
            lock (_gate)
            {
                // Load the latest library and commit first. Failed writes and the last-profile
                // guard must leave the currently rendered scene and selection untouched.
                // Deletion never installs starters or refreshes an unrelated active design.
                var store = new OverlayDesignProfileStore(_configurationFolder, OverlayRuntimeDesign.Canonicalize);
                store.Delete(profileId);
                _profiles = Array.AsReadOnly(store.Profiles.Select(profile => new OverlayDesignInfo(profile.Id, profile.Name)).ToArray());
                if (_current?.ProfileId == profileId || _configuration.ActiveOverlayDesignProfileId == profileId)
                {
                    ClearActiveDesign();
                    _status = "The active tile design was deleted. Row overlay restored.";
                }
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private OverlayDesignProfileStore Open()
        {
            var store = new OverlayDesignProfileStore(_configurationFolder, OverlayRuntimeDesign.Canonicalize);
            OverlayDesignStarterCatalog.EnsureProfiles(store);
            return store;
        }

        private string CurrentModeStatus()
        {
            string renderer = _configuration.EnableHookOverlay ? "In-game"
                : _configuration.EnableHookFreeOverlay ? "Hook-free" : "RTSS";
            return (_current == null ? "Row overlay" : "Tile overlay: " + _current.Name) + " · " + renderer;
        }

        private void SetActiveProfileId(string profileId)
        {
            if (_configuration.ActiveOverlayDesignProfileId == profileId) return;
            _updatingConfiguration = true;
            try { _configuration.ActiveOverlayDesignProfileId = profileId; }
            finally { _updatingConfiguration = false; }
        }

        private void ClearActiveDesign()
        {
            SetActiveProfileId(null);
            _current = null;
            _status = CurrentModeStatus();
            _runtimeError = false;
            _statusBeforeRuntimeError = null;
        }

        private void OnConfigurationChanged(string key)
        {
            bool refresh;
            lock (_gate)
            {
                if (_updatingConfiguration) return;
                refresh = key == nameof(IAppConfiguration.ActiveOverlayDesignProfileId);
                if (!refresh)
                {
                    if (!CanActivate) ClearActiveDesign();
                    else if (_runtimeError) _statusBeforeRuntimeError = CurrentModeStatus();
                    else _status = CurrentModeStatus();
                }
            }
            if (refresh) RefreshProfiles();
            else Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => _configurationSubscription?.Dispose();

        internal void ReportRuntimeError(string message)
        {
            lock (_gate)
            {
                // A renderer callback may finish after RTSS has already cleared its design.
                if (_current == null) return;
                if (_status == message) return;
                if (!_runtimeError) _statusBeforeRuntimeError = _status;
                _status = message;
                _runtimeError = true;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void ReportRuntimeReady()
        {
            lock (_gate)
            {
                if (!_runtimeError) return;
                _runtimeError = false;
                _status = _statusBeforeRuntimeError ?? CurrentModeStatus();
                _statusBeforeRuntimeError = null;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
