using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.View.OverlayDesignAuthoring;
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
        private Func<string> _profileErrorText;

        public OverlayDesignProfileSession ProfileSession => _profileSession;
        public bool HasUnsavedChanges => _profileSession != null
            && (_profileSession.IsDirty || Editor.HasPendingTextEdits);
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
            Editor.PendingTextEditsChanged -= OnPendingTextEditsChanged;
            Editor.PendingTextEditsChanged += OnPendingTextEditsChanged;
            AttachDesignService();
            UpdateProfileUi();
        }

        private void OnProfileHostUnloaded(object sender, RoutedEventArgs e)
        {
            _profileHostLoaded = false;
            Editor.PendingTextEditsChanged -= OnPendingTextEditsChanged;
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
                ShowProfileError(() => CxLang.Format("OverlayEditor_RuntimeRefreshFailed", OverlayDesignText.ExceptionMessage(error)));
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
            Editor.PendingTextEditsChanged += OnPendingTextEditsChanged;
            Editor.SaveRequested += OnSaveProfile;
        }

        /// <summary>The embedding host supplies its installed or portable configuration directory.</summary>
        public void ConfigureProfiles(string configurationFolder)
        {
            if (_profileStore != null || _profileInitializationAttempted)
            {
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ConfigureFirst"), message => new InvalidOperationException(message));
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
                ShowProfileError(() => CxLang.Format("OverlayEditor_LoadFailed", OverlayDesignText.ExceptionMessage(error)));
                ProfileStatus.Text = CxLang.T("OverlayEditor_ReadOnlyLibrary");
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
                    ShowProfileError(() => _profileStore.RecoveryWarning);
                }
                else if (!_profileLoadFailed && !string.IsNullOrWhiteSpace(_profileStore.StarterInitializationWarning))
                {
                    ShowProfileError(() => _profileStore.StarterInitializationWarning);
                }
            }
            catch (Exception error) when (IsProfileError(error))
            {
                _profileSession = null;
                ShowProfileError(() => CxLang.Format("OverlayEditor_LibraryUnavailable", OverlayDesignText.ExceptionMessage(error)));
                ProfileStatus.Text = CxLang.T("OverlayEditor_ReadOnlyLibrary");
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
                ShowProfileError(() => OverlayDesignText.ExceptionMessage(error));
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

        private void OnPendingTextEditsChanged(object sender, EventArgs e)
        {
            if (_updatingProfileUi || _profileSession == null || _profileLoadFailed)
                return;
            // Drafts belong to the text field until Enter, focus loss or an explicit
            // profile action. Only update the save/close affordances while typing.
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
                ShowProfileError(() => CxLang.Format("OverlayEditor_DesignLoadFailed", OverlayDesignText.ExceptionMessage(error)));
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
                ProfileDescription.Profile = null;
                return;
            }
            // The description follows the language by itself; only another profile replaces it.
            if (ProfileDescription.Profile?.Id != _profileSession.ActiveProfile.Id)
                ProfileDescription.Profile = OverlayDesignStarterCatalog.Describe(_profileSession.ActiveProfile);
            _updatingProfileUi = true;
            try
            {
                ProfileSelector.ItemsSource = _profileStore.Profiles;
                ProfileSelector.SelectedValue = _profileSession.ActiveProfile.Id;
                ProfileToolbar.IsEnabled = !_profileOperationPending && !_profileLoadFailed && !_hostIsClosing;
                DeleteProfileButton.IsEnabled = _profileStore.Profiles.Count > 1;
                bool valid = !_profileSession.HasValidationErrors;
                bool pendingText = Editor.HasPendingTextEdits;
                SaveProfileButton.IsEnabled = pendingText || valid && _profileSession.IsDirty;
                DuplicateProfileButton.IsEnabled = valid;
                ExportProfileButton.IsEnabled = valid;
                UseDesignButton.IsEnabled = valid && _designService?.CanActivate == true;
                UseDesignButton.ToolTip = _designService?.CanActivate == true
                    ? CxLang.T("OverlayEditor_UseHint")
                    : CxLang.T("OverlayEditor_RtssHint");
                ToolTipService.SetShowOnDisabled(UseDesignButton, true);
                ProfileStatus.Text = _profileLoadFailed
                    ? CxLang.T("OverlayEditor_DesignUnavailable")
                    : pendingText ? CxLang.T("OverlayEditor_UnsavedText")
                    : _profileSession.HasValidationErrors
                    ? CxLang.T("OverlayEditor_InvalidChanges")
                    : _profileSession.IsDirty ? CxLang.T("OverlayEditor_UnsavedChanges")
                    : CxLang.T("OverlayEditor_Saved");
                if (_designService != null && !_designService.CanActivate)
                    ProfileStatus.Text += " · " + CxLang.T("OverlayEditor_RtssInactive");
                UpdateShownOverlay();
            }
            finally
            {
                _updatingProfileUi = false;
            }
        }

        // What the overlay shows, which need not be the profile being edited: the active tile profile
        // while a CapFrameX renderer draws it, else the row overlay.
        private void UpdateShownOverlay()
        {
            string shownId = _designService?.IsEnabled == true && _designService.CanActivate ? _designService.ActiveProfileId : null;
            ProfileSelector.Tag = shownId;
            ShownOverlayPanel.Visibility = _designService == null ? Visibility.Collapsed : Visibility.Visible;
            ShownOverlayName.Text = shownId == null
                ? CxLang.T("OverlayDesignStatus_Row")
                : _profileStore.Profiles.FirstOrDefault(profile => profile.Id == shownId)?.Name ?? CxLang.T("OverlayDesigns_Tab");
            ShownOverlayThisProfile.Visibility = shownId != null && shownId == _profileSession.ActiveProfile.Id
                ? Visibility.Visible : Visibility.Collapsed;
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
                // Flush a focused field for Ctrl+S and host-driven close/profile
                // switches. Failed validation must still allow Cancel or Discard.
                Editor.TryCommitEdits();
                SynchronizeWorkingDesign();
                await action();
            }
            catch (Exception error) when (IsProfileError(error))
            {
                ShowProfileError(() => OverlayDesignText.ExceptionMessage(error));
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
            var prompt = new OverlayDesignPrompt(() => CxLang.T("OverlayEditor_SaveChangesTitle"),
                () => _profileSession.HasValidationErrors
                    ? CxLang.Format("OverlayEditor_SaveInvalidMessage", _profileSession.ActiveProfile.Name)
                    : CxLang.Format("OverlayEditor_SaveChangesMessage", _profileSession.ActiveProfile.Name),
                () => CxLang.T("OverlayEditor_Save"), secondary: () => CxLang.T("OverlayEditor_Discard"), canConfirm: !_profileSession.HasValidationErrors);
            var result = await DialogHost.Show(prompt, _profileDialogId);
            return result is OverlayDesignPromptResult.Confirm ? OverlayDesignUnsavedChangesDecision.Save
                : result is OverlayDesignPromptResult.Secondary ? OverlayDesignUnsavedChangesDecision.Discard
                : OverlayDesignUnsavedChangesDecision.Cancel;
        }

        private async Task<string> AskProfileNameAsync(Func<string> title, string initialName, string currentId = null,
            Func<string> message = null)
        {
            var prompt = new OverlayDesignPrompt(title, message ?? (() => CxLang.T("OverlayEditor_ChooseName")), () => CxLang.T("OverlayEditor_Save"), initialName,
                name => name.Length > OverlayDesignProfileStore.MaximumNameLength
                    ? CxLang.Format("OverlayEditor_NameLimit", OverlayDesignProfileStore.MaximumNameLength)
                    : _profileStore.Profiles.Any(profile => profile.Id != currentId && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                        ? CxLang.T("OverlayEditor_NameExists") : null);
            return await DialogHost.Show(prompt, _profileDialogId) is OverlayDesignPromptResult.Confirm
                ? prompt.EnteredName : null;
        }

        private string SuggestName(string basis)
        {
            basis = string.IsNullOrWhiteSpace(basis) ? CxLang.T("OverlayEditor_NewDesign") : basis.Trim();
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
                        throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ProfileGone"), message => new InvalidOperationException(message));
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
                        throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_InvalidValues"), message => new InvalidOperationException(message));
                    var saved = refreshed.Profiles.FirstOrDefault(profile => profile.Id == _profileSession.ActiveProfile.Id);
                    if (saved == null || saved.DesignJson != _profileSession.ActiveProfile.DesignJson)
                        throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ChangedElsewhere"), message => new InvalidOperationException(message));
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

        /// <summary>
        /// Rereads a library that another writer changed and shows <paramref name="profileId"/>.
        /// Declines while the user has edits of their own: their working copy always wins.
        /// </summary>
        public Task<bool> ReloadLibraryAsync(string profileId)
        {
            if (_profileSession == null || _profileLoadFailed || _profileOperationPending || _hostIsClosing || HasUnsavedChanges)
                return Task.FromResult(false);
            return OpenProfileAsync(string.IsNullOrWhiteSpace(profileId) ? _profileSession.ActiveProfile.Id : profileId);
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
                string name = await AskProfileNameAsync(() => CxLang.T("OverlayEditor_NewDesign"), SuggestName(CxLang.T("OverlayEditor_NewDesign")));
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
                string name = await AskProfileNameAsync(() => CxLang.T("OverlayEditor_DuplicateDesign"), SuggestName(CxLang.Format("OverlayEditor_CopyName", _profileSession.ActiveProfile.Name)));
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
                string name = await AskProfileNameAsync(() => CxLang.T("OverlayEditor_RenameDesign"), _profileSession.ActiveProfile.Name, _profileSession.ActiveProfile.Id);
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
                var prompt = new OverlayDesignPrompt(() => CxLang.T("OverlayEditor_DeleteTitle"),
                    () => _profileSession.IsDirty
                        ? CxLang.Format("OverlayEditor_DeleteDirtyMessage", _profileSession.ActiveProfile.Name)
                        : CxLang.Format("OverlayEditor_DeleteMessage", _profileSession.ActiveProfile.Name),
                    () => CxLang.T("OverlayEditor_Delete"));
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
                var dialog = new OpenFileDialog { Filter = CxLang.T("OverlayEditor_FileFilter"), CheckFileExists = true };
                if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                {
                    return;
                }
                // The design is adapted to this machine's hardware; without its sources it stays as it came.
                var imported = OverlayDesignProfileTransfer.Import(dialog.FileName, await GetImportSourcesAsync());
                string name = await AskProfileNameAsync(() => CxLang.T("OverlayEditor_ImportDesign"), SuggestName(imported.Name),
                    message: () => DescribeImport(imported) + Environment.NewLine + Environment.NewLine + CxLang.T("OverlayEditor_ChooseName"));
                if (name == null)
                {
                    return;
                }
                var document = OsdDesignDocument.FromJson(imported.DesignJson);
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
                    Filter = CxLang.T("OverlayEditor_FileFilter"), FileName = name + ".json",
                    DefaultExt = ".json", AddExtension = true
                };
                if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                {
                    OverlayDesignProfileTransfer.Export(json, dialog.FileName);
                }
                return Task.CompletedTask;
            });
        }

        private async Task<IReadOnlyList<OverlayTelemetrySource>> GetImportSourcesAsync()
        {
            var service = TelemetryService;
            if (service == null)
            {
                return null;
            }
            try
            {
                return await service.GetSourcesAsync();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // The source status reports the failed discovery; the design is imported unchanged.
                return null;
            }
        }

        private static string DescribeImport(OverlayDesignImportResult imported)
        {
            var lines = new List<string>();
            var adapted = imported.Devices.Where(device => device.TargetDevice != null && device.TargetDevice != device.SourceDevice)
                .Select(device => device.SourceDevice + " → " + device.TargetDevice).ToArray();
            if (!imported.IsMapped)
            {
                lines.Add(CxLang.T("OverlayEditor_ImportUnmapped"));
            }
            else if (imported.IsGeneric)
            {
                lines.Add(CxLang.T("OverlayEditor_ImportGeneric"));
            }
            else if (adapted.Length > 0)
            {
                lines.Add(CxLang.Format("OverlayEditor_ImportAdapted", string.Join(", ", adapted)));
            }
            else if (imported.Unresolved.Count == 0)
            {
                lines.Add(CxLang.T("OverlayEditor_ImportSameHardware"));
            }
            if (imported.IsMapped && imported.Unresolved.Count > 0)
            {
                const int listed = 8;
                var readings = imported.Unresolved.Take(listed)
                    .Select(reading => DescribeTile(reading) + " (" + DescribeIssue(reading.Issue) + ")").ToList();
                if (imported.Unresolved.Count > listed)
                {
                    readings.Add("…");
                }
                lines.Add(CxLang.Format("OverlayEditor_ImportUnresolved", imported.Unresolved.Count, string.Join(", ", readings)));
            }
            return string.Join(Environment.NewLine, lines);
        }

        // A tile without a label is named by its sensor.
        private static string DescribeTile(OverlayDesignUnresolvedReading reading)
        {
            if (!string.IsNullOrWhiteSpace(reading.Tile))
            {
                return reading.Tile;
            }
            return OverlaySensorSources.TryParseSensorId(reading.Key, out _, out _, out string sensor)
                ? sensor : CxLang.T("OverlayEditor_ImportUntitledTile");
        }

        private static string DescribeIssue(OverlayDesignImportIssue issue) => issue switch
        {
            OverlayDesignImportIssue.NoMatchingDevice => CxLang.T("OverlayEditor_ImportIssue_NoMatchingDevice"),
            OverlayDesignImportIssue.AmbiguousDevice => CxLang.T("OverlayEditor_ImportIssue_AmbiguousDevice"),
            OverlayDesignImportIssue.MissingCoreType => CxLang.T("OverlayEditor_ImportIssue_MissingCoreType"),
            OverlayDesignImportIssue.UnknownDevice => CxLang.T("OverlayEditor_ImportIssue_UnknownDevice"),
            _ => CxLang.T("OverlayEditor_ImportIssue_MissingReading")
        };

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

        private void ShowProfileError(Func<string> message)
        {
            _profileErrorText = message;
            ProfileError.Text = message();
            ProfileError.Visibility = Visibility.Visible;
        }

        private void ClearProfileError()
        {
            _profileErrorText = null;
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
