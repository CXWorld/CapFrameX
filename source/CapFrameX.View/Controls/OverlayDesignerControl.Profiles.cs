using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace CapFrameX.View.Controls
{
    public partial class OverlayDesignerControl
    {
        private readonly string _profileDialogId = "OverlayDesign-" + Guid.NewGuid().ToString("N");
        private OverlayDesignProfileStore _profileStore;
        private string _profileConfigurationFolder;
        private OverlayDesignProfileSession _profileSession;
        private bool _updatingProfileUi;
        private bool _profileOperationPending;
        private bool _profileInitializationAttempted;
        private bool _newProfileNeedsSourceBinding;
        private string _starterProfileId;
        private bool _profileLoadFailed;
        private bool _hostIsClosing;
        private IOverlayDesignService _designService;
        private bool _profileHostLoaded;
        private bool _designServiceSubscribed;
        private int _designServiceGeneration;

        public OverlayDesignProfileSession ProfileSession => _profileSession;
        public bool HasUnsavedChanges => _profileSession?.IsDirty == true;
        public bool IsProfileOperationPending => _profileOperationPending;

        public void ConfigureDesignService(IOverlayDesignService service)
        {
            DetachDesignService();
            _designService = service;
            UseDesignButton.Visibility = service == null ? Visibility.Collapsed : Visibility.Visible;
            if (_profileHostLoaded) AttachDesignService();
            UpdateProfileUi();
        }

        private void OnProfileHostLoaded(object sender, RoutedEventArgs e)
        {
            _profileHostLoaded = true;
            AttachDesignService();
            UpdateProfileUi();
        }

        private void OnProfileHostUnloaded(object sender, RoutedEventArgs e)
        {
            _profileHostLoaded = false;
            DetachDesignService();
        }

        private void AttachDesignService()
        {
            if (_designService == null || _designServiceSubscribed) return;
            Interlocked.Increment(ref _designServiceGeneration);
            _designService.Changed += OnRuntimeDesignChanged;
            _designServiceSubscribed = true;
        }

        private void DetachDesignService()
        {
            Interlocked.Increment(ref _designServiceGeneration);
            if (_designServiceSubscribed)
            {
                _designService.Changed -= OnRuntimeDesignChanged;
                _designServiceSubscribed = false;
            }
        }

        private void OnRuntimeDesignChanged(object sender, EventArgs e)
        {
            int generation = Volatile.Read(ref _designServiceGeneration);
            void Update()
            {
                // A runtime publication can already be queued when the editor unloads or
                // changes service. Never revive that subscription or touch an old host.
                if (_profileHostLoaded && _designServiceSubscribed && generation == _designServiceGeneration
                    && ReferenceEquals(sender, _designService))
                    UpdateProfileUi();
            }
            if (Dispatcher.CheckAccess()) Update();
            else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                _ = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(Update));
        }

        private void RefreshRuntimeProfiles()
        {
            try
            {
                _designService?.RefreshProfiles();
            }
            catch (Exception error) when (IsProfileError(error))
            {
                ShowProfileError("The saved design could not be refreshed in the overlay. " + error.Message);
            }
        }

        private async void OnUseDesign(object sender, RoutedEventArgs e)
        {
            if (_designService?.CanActivate != true)
                return;
            await RunProfileActionAsync(async () =>
            {
                if (!_profileSession.TryClose(await AskUnsavedChangesAsync()))
                    return;
                LoadSelectedProfile();
                if (_profileLoadFailed)
                    return;
                _designService.RefreshProfiles();
                _designService.Activate(_profileSession.ActiveProfile.Id);
            });
        }

        /// <summary>Freezes edits while an owner resolves asynchronous shutdown prompts.</summary>
        public void SetHostClosing(bool closing)
        {
            _hostIsClosing = closing;
            UpdateProfileUi();
        }

        private void InitializeProfiles()
        {
            Loaded += OnProfileHostLoaded;
            Unloaded += OnProfileHostUnloaded;
            ProfileDialogHost.Identifier = _profileDialogId;
            Editor.HostManagesFiles = true;
            Editor.DocumentEdited += OnDesignEdited;
            Editor.TemplateChanged += OnDesignEdited;
            Editor.SaveRequested += OnSaveProfile;
        }

        /// <summary>The embedding host supplies its installed or portable configuration directory.</summary>
        public void ConfigureProfiles(string configurationFolder)
        {
            if (_profileStore != null || _profileInitializationAttempted)
            {
                throw new InvalidOperationException("Configure the profile library before loading the designer.");
            }
            try
            {
                _profileStore = new OverlayDesignProfileStore(configurationFolder,
                    json => OsdDesignDocument.FromJson(json).ToJson());
                _profileConfigurationFolder = configurationFolder;
                OverlayDesignStarterCatalog.EnsureProfiles(_profileStore);
                _newProfileNeedsSourceBinding = _profileStore.Profiles.Count == 0;
                EnsureProfilesInitialized();
            }
            catch (Exception error) when (IsProfileError(error))
            {
                ShowProfileError("Saved designs could not be loaded. " + error.Message);
                ProfileStatus.Text = "Profile library unavailable · Editor is read-only. Existing files have been preserved.";
                Editor.IsEnabled = false;
            }
        }

        private void EnsureProfilesInitialized()
        {
            if (_profileStore == null || _profileInitializationAttempted)
            {
                return;
            }
            _profileInitializationAttempted = true;
            try
            {
                var session = new OverlayDesignProfileSession(_profileStore);
                session.Initialize("Benchmark", Editor.GetTemplateJson());
                _profileSession = session;
                if (_newProfileNeedsSourceBinding)
                {
                    _starterProfileId = session.ActiveProfile.Id;
                }
                LoadSelectedProfile();
                RefreshRuntimeProfiles();
                if (!_profileLoadFailed && !string.IsNullOrWhiteSpace(_profileStore.RecoveryWarning))
                {
                    ShowProfileError(_profileStore.RecoveryWarning);
                }
                else if (!_profileLoadFailed && !string.IsNullOrWhiteSpace(_profileStore.StarterInitializationWarning))
                {
                    ShowProfileError(_profileStore.StarterInitializationWarning);
                }
            }
            catch (Exception error) when (IsProfileError(error))
            {
                _profileSession = null;
                ShowProfileError("The profile library is unavailable. " + error.Message);
                ProfileStatus.Text = "Profile library unavailable · Editor is read-only. Existing files have been preserved.";
                Editor.IsEnabled = false;
            }
        }

        private void CompleteInitialProfileBinding()
        {
            if (!_newProfileNeedsSourceBinding || _profileSession == null || Editor.Sources == null || !Editor.Sources.Any(source => source.IsAvailable)
                || _hostIsClosing || _profileLoadFailed)
            {
                return;
            }
            _newProfileNeedsSourceBinding = false;
            if (_profileSession.ActiveProfile.Id != _starterProfileId)
            {
                return;
            }
            bool hadChanges = _profileSession.IsDirty;
            try
            {
                // Resolve only an untouched shipped starter (or the initial fallback).
                // Edited, imported and previously bound profiles retain their exact bindings.
                Editor.Document.BindPresetSources(Editor.Sources);
                SynchronizeWorkingDesign();
                if (!hadChanges && _profileSession.IsDirty && !_profileSession.HasValidationErrors)
                {
                    _profileSession.Save();
                    // Initial hardware binding is the saved baseline, not a user edit that
                    // Undo should turn back into unresolved starter placeholders.
                    if (Editor.TryCommitEdits()) Editor.ResetHistory();
                }
                UpdateProfileUi();
                RefreshRuntimeProfiles();
            }
            catch (Exception error) when (IsProfileError(error))
            {
                ShowProfileError(error.Message);
            }
        }

        private void OnDesignEdited(object sender, EventArgs e)
        {
            if (_updatingProfileUi || _profileSession == null || _profileLoadFailed)
            {
                return;
            }
            SynchronizeWorkingDesign();
            UpdateProfileUi();
        }

        private void SynchronizeWorkingDesign()
        {
            if (_profileSession == null || _profileLoadFailed)
            {
                return;
            }
            try
            {
                // A profile owns its name; undoing a template choice or a rename must not
                // silently rename it or make a just-saved profile dirty again.
                _updatingProfileUi = true;
                Editor.Document.Name = _profileSession.ActiveProfile.Name;
                _profileSession.SetWorkingTemplate(Editor.GetTemplateJson());
            }
            catch (Exception error) when (IsProfileError(error))
            {
                _profileSession.MarkInvalidEdit();
            }
            finally
            {
                _updatingProfileUi = false;
            }
        }

        private void LoadSelectedProfile()
        {
            _updatingProfileUi = true;
            try
            {
                Editor.SetTemplateJson(_profileSession.WorkingTemplateJson);
                Editor.ResetHistory();
                _profileLoadFailed = false;
                if (!_profileSession.IsDirty && OverlayDesignStarterCatalog.IsUnmodifiedStarter(_profileSession.ActiveProfile))
                {
                    _starterProfileId = _profileSession.ActiveProfile.Id;
                    _newProfileNeedsSourceBinding = true;
                }
            }
            catch (Exception error) when (IsProfileError(error))
            {
                // The neutral editor keeps its previous accepted document on rejection.
                // It must never be synchronized into the newly selected profile.
                _profileLoadFailed = true;
                ShowProfileError("This design could not be loaded. The editor is read-only; reopen the designer to retry. " + error.Message);
            }
            finally
            {
                _updatingProfileUi = false;
                UpdateProfileUi();
            }
            CompleteInitialProfileBinding();
        }

        private void UpdateProfileUi()
        {
            Editor.IsEnabled = _profileSession != null && !_profileLoadFailed && !_hostIsClosing;
            if (_profileSession?.ActiveProfile == null)
            {
                return;
            }
            _updatingProfileUi = true;
            try
            {
                ProfileSelector.ItemsSource = _profileStore.Profiles;
                ProfileSelector.SelectedValue = _profileSession.ActiveProfile.Id;
                ProfileToolbar.IsEnabled = !_profileOperationPending && !_profileLoadFailed && !_hostIsClosing;
                DeleteProfileButton.IsEnabled = _profileStore.Profiles.Count > 1;
                bool valid = !_profileSession.HasValidationErrors;
                SaveProfileButton.IsEnabled = valid && _profileSession.IsDirty;
                DuplicateProfileButton.IsEnabled = valid;
                ExportProfileButton.IsEnabled = valid;
                UseDesignButton.IsEnabled = valid && _designService?.CanActivate == true;
                UseDesignButton.ToolTip = _designService?.CanActivate == true
                    ? "Show this saved tile profile with the selected CapFrameX renderer."
                    : "RTSS uses Row overlay. Select In-game or Hook-free in Overlay settings to activate Tile overlay.";
                ToolTipService.SetShowOnDisabled(UseDesignButton, true);
                ProfileStatus.Text = _profileLoadFailed
                    ? "Design unavailable · Saved profiles have been preserved."
                    : _profileSession.HasValidationErrors
                    ? "Unsaved changes · Correct the highlighted values before saving."
                    : _profileSession.IsDirty ? "Unsaved changes · Ctrl+S to save."
                    : "Saved · This design will reopen next time.";
                if (_designService?.IsEnabled == true && _designService.ActiveProfileId == _profileSession.ActiveProfile.Id)
                    ProfileStatus.Text += " · Tile overlay active";
                if (_designService != null && !_designService.CanActivate)
                    ProfileStatus.Text += " · RTSS: Tile overlay is off. Select In-game or Hook-free to activate a design.";
            }
            finally
            {
                _updatingProfileUi = false;
            }
        }

        private async Task RunProfileActionAsync(Func<Task> action, bool closing = false)
        {
            if (_profileSession == null || _profileOperationPending || _profileLoadFailed
                || (_hostIsClosing && !closing))
            {
                return;
            }
            _profileOperationPending = true;
            UpdateProfileUi();
            try
            {
                ClearProfileError();
                SynchronizeWorkingDesign();
                await action();
            }
            catch (Exception error) when (IsProfileError(error))
            {
                ShowProfileError(error.Message);
            }
            finally
            {
                _profileOperationPending = false;
                UpdateProfileUi();
                RefreshRuntimeProfiles();
            }
        }

        private async Task<OverlayDesignUnsavedChangesDecision> AskUnsavedChangesAsync()
        {
            if (!_profileSession.IsDirty)
            {
                return OverlayDesignUnsavedChangesDecision.Discard;
            }
            var prompt = new OverlayDesignPrompt("Save changes?",
                $"“{_profileSession.ActiveProfile.Name}” has unsaved changes."
                    + (_profileSession.HasValidationErrors ? " Some values are invalid. Cancel to correct them, or discard the changes." : " Save them before continuing?"),
                "Save", secondary: "Discard", canConfirm: !_profileSession.HasValidationErrors);
            var result = await DialogHost.Show(prompt, _profileDialogId);
            return result is OverlayDesignPromptResult.Confirm ? OverlayDesignUnsavedChangesDecision.Save
                : result is OverlayDesignPromptResult.Secondary ? OverlayDesignUnsavedChangesDecision.Discard
                : OverlayDesignUnsavedChangesDecision.Cancel;
        }

        private async Task<string> AskProfileNameAsync(string title, string initialName, string currentId = null)
        {
            var prompt = new OverlayDesignPrompt(title, "Choose a name for this design.", "Save", initialName,
                name => name.Length > OverlayDesignProfileStore.MaximumNameLength
                    ? $"Use at most {OverlayDesignProfileStore.MaximumNameLength} characters."
                    : _profileStore.Profiles.Any(profile => profile.Id != currentId && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                        ? "A design with this name already exists." : null);
            return await DialogHost.Show(prompt, _profileDialogId) is OverlayDesignPromptResult.Confirm
                ? prompt.EnteredName : null;
        }

        private string SuggestName(string basis)
        {
            basis = string.IsNullOrWhiteSpace(basis) ? "New design" : basis.Trim();
            basis = basis.Substring(0, Math.Min(basis.Length, OverlayDesignProfileStore.MaximumNameLength - 8));
            string result = basis;
            for (int number = 2; _profileStore.Profiles.Any(profile => string.Equals(profile.Name, result, StringComparison.OrdinalIgnoreCase)); number++)
            {
                result = basis + " " + number;
            }
            return result;
        }

        private async void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingProfileUi || _profileOperationPending || _profileSession == null
                || ProfileSelector.SelectedItem is not OverlayDesignProfile selected
                || selected.Id == _profileSession.ActiveProfile.Id)
            {
                return;
            }
            await OpenProfileAsync(selected.Id);
        }

        /// <summary>Opens a saved profile by ID without replacing unapproved working edits.</summary>
        public async Task<bool> OpenProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;
            bool opened = false;
            await RunProfileActionAsync(async () =>
            {
                // The library can change through the main page while this editor remains open.
                // Read it independently so cancellation never changes this session or selection.
                OverlayDesignProfileStore ReadLibrary()
                {
                    var library = new OverlayDesignProfileStore(_profileConfigurationFolder,
                        json => OsdDesignDocument.FromJson(json).ToJson());
                    if (!library.Profiles.Any(profile => profile.Id == profileId))
                        throw new InvalidOperationException("This saved tile profile no longer exists. Refresh the profile list and select another profile.");
                    return library;
                }
                ReadLibrary();
                if (_profileSession.ActiveProfile.Id == profileId && _profileSession.IsDirty)
                {
                    // Opening the profile already being edited must preserve its working copy.
                    opened = true;
                    return;
                }
                var decision = await AskUnsavedChangesAsync();
                if (decision == OverlayDesignUnsavedChangesDecision.Cancel)
                    return;
                var refreshed = ReadLibrary();
                if (_profileSession.IsDirty && decision == OverlayDesignUnsavedChangesDecision.Save)
                {
                    if (_profileSession.HasValidationErrors)
                        throw new InvalidOperationException("Correct the invalid design values before saving, or discard your changes.");
                    var saved = refreshed.Profiles.FirstOrDefault(profile => profile.Id == _profileSession.ActiveProfile.Id);
                    if (saved == null || saved.DesignJson != _profileSession.ActiveProfile.DesignJson)
                        throw new InvalidOperationException("The profile being edited was changed or deleted elsewhere. Your edits are still open; cancel to keep editing, or discard them when opening another profile.");
                    refreshed.Save(saved.Id, _profileSession.WorkingTemplateJson);
                }
                refreshed.SetActive(profileId);
                var session = new OverlayDesignProfileSession(refreshed);
                session.Initialize("Benchmark", refreshed.Get(profileId).DesignJson);
                _profileStore = refreshed;
                _profileSession = session;
                _newProfileNeedsSourceBinding = false;
                LoadSelectedProfile();
                opened = !_profileLoadFailed;
            });
            return opened;
        }

        private async void OnSaveProfile(object sender, EventArgs e)
        {
            await RunProfileActionAsync(() =>
            {
                _profileSession.Save();
                _newProfileNeedsSourceBinding = false;
                return Task.CompletedTask;
            });
        }

        private async void OnNewProfile(object sender, RoutedEventArgs e)
        {
            await RunProfileActionAsync(async () =>
            {
                string name = await AskProfileNameAsync("New design", SuggestName("New design"));
                if (name == null)
                {
                    return;
                }
                var document = OsdDesignDocument.CreatePreset("Minimal");
                document.Name = name;
                document.EnableCanvas();
                document.BindPresetSources(Editor.Sources);
                if (_profileSession.Create(name, document.ToJson(), await AskUnsavedChangesAsync()))
                {
                    _newProfileNeedsSourceBinding = false;
                    LoadSelectedProfile();
                }
            });
        }

        private async void OnDuplicateProfile(object sender, RoutedEventArgs e)
        {
            await RunProfileActionAsync(async () =>
            {
                string name = await AskProfileNameAsync("Duplicate design", SuggestName(_profileSession.ActiveProfile.Name + " copy"));
                if (name != null)
                {
                    _profileSession.Duplicate(name);
                    _newProfileNeedsSourceBinding = false;
                    LoadSelectedProfile();
                }
            });
        }

        private async void OnRenameProfile(object sender, RoutedEventArgs e)
        {
            await RunProfileActionAsync(async () =>
            {
                string name = await AskProfileNameAsync("Rename design", _profileSession.ActiveProfile.Name, _profileSession.ActiveProfile.Id);
                if (name != null)
                {
                    _profileSession.Rename(name);
                    _updatingProfileUi = true;
                    try
                    {
                        Editor.Document.Name = name;
                    }
                    finally
                    {
                        _updatingProfileUi = false;
                    }
                }
            });
        }

        private async void OnDeleteProfile(object sender, RoutedEventArgs e)
        {
            await RunProfileActionAsync(async () =>
            {
                var prompt = new OverlayDesignPrompt("Delete design?",
                    $"Delete “{_profileSession.ActiveProfile.Name}” from your saved designs?"
                        + (_profileSession.IsDirty ? " Its unsaved changes will also be discarded." : ""), "Delete");
                if (await DialogHost.Show(prompt, _profileDialogId) is OverlayDesignPromptResult.Confirm
                    && _profileSession.DeleteActive(OverlayDesignUnsavedChangesDecision.Discard))
                {
                    _newProfileNeedsSourceBinding = false;
                    LoadSelectedProfile();
                }
            });
        }

        private async void OnImportProfile(object sender, RoutedEventArgs e)
        {
            await RunProfileActionAsync(async () =>
            {
                var dialog = new OpenFileDialog { Filter = "CapFrameX overlay design (*.json)|*.json", CheckFileExists = true };
                if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                {
                    return;
                }
                if (new FileInfo(dialog.FileName).Length > OsdDesignDocument.MaximumJsonLength)
                {
                    throw new FormatException("The design file exceeds 1 MB.");
                }
                var document = OsdDesignDocument.FromJson(File.ReadAllText(dialog.FileName));
                string name = await AskProfileNameAsync("Import design", SuggestName(document.Name));
                if (name == null)
                {
                    return;
                }
                document.Name = name;
                if (_profileSession.Create(name, document.ToJson(), await AskUnsavedChangesAsync()))
                {
                    _newProfileNeedsSourceBinding = false;
                    LoadSelectedProfile();
                }
            });
        }

        private async void OnExportProfile(object sender, RoutedEventArgs e)
        {
            await RunProfileActionAsync(() =>
            {
                string json = Editor.GetTemplateJson();
                string name = _profileSession.ActiveProfile.Name;
                foreach (char invalid in Path.GetInvalidFileNameChars())
                {
                    name = name.Replace(invalid, '_');
                }
                var dialog = new SaveFileDialog
                {
                    Filter = "CapFrameX overlay design (*.json)|*.json", FileName = name + ".json",
                    DefaultExt = ".json", AddExtension = true
                };
                if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                {
                    // A failed export must leave a previous file intact.
                    string temporary = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temporary, json, new UTF8Encoding(false));
                        File.Move(temporary, dialog.FileName, true);
                    }
                    finally
                    {
                        if (File.Exists(temporary))
                        {
                            File.Delete(temporary);
                        }
                    }
                }
                return Task.CompletedTask;
            });
        }

        /// <summary>Hosts call this before removing the designer or closing its window.</summary>
        public async Task<bool> TryCloseAsync()
        {
            if (_profileOperationPending)
            {
                return false;
            }
            if (_profileSession == null || _profileLoadFailed)
            {
                return true;
            }
            bool canClose = false;
            await RunProfileActionAsync(async () =>
            {
                canClose = _profileSession.TryClose(await AskUnsavedChangesAsync());
                if (canClose)
                {
                    LoadSelectedProfile();
                }
            }, closing: true);
            return canClose;
        }

        private void ShowProfileError(string message)
        {
            ProfileError.Text = message;
            ProfileError.Visibility = Visibility.Visible;
        }

        private void ClearProfileError()
        {
            ProfileError.Text = string.Empty;
            ProfileError.Visibility = Visibility.Collapsed;
        }

        private static bool IsProfileError(Exception error) => error is IOException || error is InvalidDataException
            || error is UnauthorizedAccessException || error is FormatException
            || error is InvalidOperationException || error is ArgumentException
            || error is NotSupportedException
            || error is Newtonsoft.Json.JsonException || error is System.Text.Json.JsonException;
    }
}
