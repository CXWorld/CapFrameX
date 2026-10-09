using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;
using CapFrameX.EventAggregation.Messages;
using Prism.Commands;
using Prism.Events;

namespace CapFrameX.ViewModel
{
    public partial class OverlayViewModel
    {
        private IOverlayDesignService _designService;
        private OverlayDesignInfo _selectedDesign;
        private string _designActionError;
        private int _selectedOverlayTabIndex;
        private OverlayDesignInfo _pendingDesignDeletion;
        private bool _isDeleteDesignDialogOpen;

        public IReadOnlyList<OverlayDesignInfo> SavedDesigns => _designService?.Profiles ?? Array.Empty<OverlayDesignInfo>();
        public string DesignStatus => _designActionError ?? _designService?.Status ?? string.Empty;
        public bool HasActiveDesign => _designService?.IsEnabled == true;
        public bool IsTileOverlayAvailable => !OverlayModeRtss;
        public bool CanUseDesign => IsTileOverlayAvailable && _designService?.CanActivate == true;
        public bool HasSavedDesigns => SavedDesigns.Count > 0;
        public int SelectedOverlayTabIndex
        {
            get => _selectedOverlayTabIndex;
            set
            {
                int selected = value == 1 && !IsTileOverlayAvailable ? 0 : value;
                if (!SetProperty(ref _selectedOverlayTabIndex, selected) && selected != value)
                    RaisePropertyChanged();
                if (selected != 1) IsDeleteDesignDialogOpen = false;
            }
        }
        public OverlayDesignInfo SelectedDesign
        {
            get => _selectedDesign;
            set
            {
                if (SetProperty(ref _selectedDesign, value))
                    UseDesignCommand?.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand OpenDesignerCommand { get; private set; }
        public DelegateCommand UseDesignCommand { get; private set; }
        public DelegateCommand UseClassicOverlayCommand { get; private set; }
        public DelegateCommand RefreshDesignsCommand { get; private set; }
        public DelegateCommand<OverlayDesignInfo> EditSavedDesignCommand { get; private set; }
        public DelegateCommand<OverlayDesignInfo> ShowSavedDesignCommand { get; private set; }
        public DelegateCommand<OverlayDesignInfo> DeleteSavedDesignCommand { get; private set; }
        public DelegateCommand ConfirmDeleteDesignCommand { get; private set; }
        public DelegateCommand CancelDeleteDesignCommand { get; private set; }

        public bool IsDeleteDesignDialogOpen
        {
            get => _isDeleteDesignDialogOpen;
            set
            {
                if (!SetProperty(ref _isDeleteDesignDialogOpen, value)) return;
                if (!value) _pendingDesignDeletion = null;
                RaisePropertyChanged(nameof(DeleteDesignMessage));
                RaisePropertyChanged(nameof(IsDeletingActiveDesign));
                UpdateSavedDesignCommands();
            }
        }

        public string DeleteDesignMessage => CxLang.Format("OverlayDesigns_DeleteMessage", _pendingDesignDeletion?.Name ?? string.Empty);
        public bool IsDeletingActiveDesign => _pendingDesignDeletion != null
            && _pendingDesignDeletion.Id == _designService?.ActiveProfileId;

        private void InitializeDesigns(IOverlayDesignService service, IEventAggregator events)
        {
            _designService = service;
            OpenDesignerCommand = new DelegateCommand(() =>
                events?.GetEvent<PubSubEvent<ViewMessages.OpenOverlayDesigner>>()
                    .Publish(new ViewMessages.OpenOverlayDesigner()), () => events != null);
            UseDesignCommand = new DelegateCommand(() =>
            {
                if (CanUseDesign && SelectedDesign != null)
                    RunDesignAction(() => _designService.Activate(SelectedDesign.Id));
            }, () => CanUseDesign && SelectedDesign != null);
            UseClassicOverlayCommand = new DelegateCommand(() => RunDesignAction(() =>
                _designService.Deactivate()), () => HasActiveDesign);
            RefreshDesignsCommand = new DelegateCommand(RefreshDesigns);
            EditSavedDesignCommand = new DelegateCommand<OverlayDesignInfo>(profile =>
            {
                if (!EditSavedDesignCommand.CanExecute(profile)) return;
                SelectedDesign = FindSavedDesign(profile);
                events.GetEvent<PubSubEvent<ViewMessages.OpenOverlayDesigner>>()
                    .Publish(new ViewMessages.OpenOverlayDesigner { ProfileId = profile.Id });
            }, profile => events != null && IsTileOverlayAvailable && !IsDeleteDesignDialogOpen && FindSavedDesign(profile) != null);
            ShowSavedDesignCommand = new DelegateCommand<OverlayDesignInfo>(profile =>
            {
                if (!ShowSavedDesignCommand.CanExecute(profile)) return;
                SelectedDesign = FindSavedDesign(profile);
                RunDesignAction(() => _designService.Activate(profile.Id));
            }, profile => CanUseDesign && !IsDeleteDesignDialogOpen && FindSavedDesign(profile) != null);
            DeleteSavedDesignCommand = new DelegateCommand<OverlayDesignInfo>(profile =>
            {
                if (!DeleteSavedDesignCommand.CanExecute(profile)) return;
                // Capture the context-menu target before opening the dialog. Neither list
                // selection nor an intervening refresh may redirect the eventual deletion.
                _pendingDesignDeletion = FindSavedDesign(profile);
                SelectedDesign = _pendingDesignDeletion;
                IsDeleteDesignDialogOpen = true;
            }, profile => IsTileOverlayAvailable && !IsDeleteDesignDialogOpen && SavedDesigns.Count > 1 && FindSavedDesign(profile) != null);
            ConfirmDeleteDesignCommand = new DelegateCommand(() =>
            {
                if (!ConfirmDeleteDesignCommand.CanExecute()) return;
                string profileId = _pendingDesignDeletion.Id;
                IsDeleteDesignDialogOpen = false;
                RunDesignAction(() => _designService.Delete(profileId));
            }, () => IsTileOverlayAvailable && IsDeleteDesignDialogOpen && SavedDesigns.Count > 1 && FindSavedDesign(_pendingDesignDeletion) != null);
            CancelDeleteDesignCommand = new DelegateCommand(() => IsDeleteDesignDialogOpen = false);
            if (service != null)
                service.Changed += OnDesignServiceChanged;
            UpdateDesignSelection();
        }

        private void OnDesignServiceChanged(object sender, EventArgs e)
        {
            void Update()
            {
                _designActionError = null;
                UpdateDesignSelection();
            }
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                Update();
            else
                _ = dispatcher.BeginInvoke(new Action(Update));
        }

        private void UpdateDesignSelection()
        {
            string previous = SelectedDesign?.Id;
            RaisePropertyChanged(nameof(SavedDesigns));
            SelectedDesign = SavedDesigns.FirstOrDefault(profile => profile.Id == previous)
                ?? SavedDesigns.FirstOrDefault(profile => profile.Id == _designService?.ActiveProfileId)
                ?? SavedDesigns.FirstOrDefault();
            RaisePropertyChanged(nameof(DesignStatus));
            RaisePropertyChanged(nameof(IsOverlayActive));
            RaisePropertyChanged(nameof(HasActiveDesign));
            RaisePropertyChanged(nameof(HasSavedDesigns));
            UpdateTileOverlayAvailability();
            RaisePropertyChanged(nameof(CanUseDesign));
            UseDesignCommand?.RaiseCanExecuteChanged();
            UseClassicOverlayCommand?.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(IsDeletingActiveDesign));
            UpdateSavedDesignCommands();
        }

        private OverlayDesignInfo FindSavedDesign(OverlayDesignInfo profile) => profile == null ? null
            : SavedDesigns.FirstOrDefault(saved => saved.Id == profile.Id);

        private void UpdateSavedDesignCommands()
        {
            EditSavedDesignCommand?.RaiseCanExecuteChanged();
            ShowSavedDesignCommand?.RaiseCanExecuteChanged();
            DeleteSavedDesignCommand?.RaiseCanExecuteChanged();
            ConfirmDeleteDesignCommand?.RaiseCanExecuteChanged();
        }

        private void UpdateTileOverlayAvailability()
        {
            // Select the visible row editor before the tile tab collapses. Returning to a
            // native renderer makes the tab available without switching content or selection.
            if (!IsTileOverlayAvailable && SelectedOverlayTabIndex == 1)
                SelectedOverlayTabIndex = 0;
            if (!IsTileOverlayAvailable) IsDeleteDesignDialogOpen = false;
            RaisePropertyChanged(nameof(IsTileOverlayAvailable));
        }

        private void RefreshDesigns()
        {
            if (_designService != null)
                RunDesignAction(_designService.RefreshProfiles);
        }

        private void RunDesignAction(Action action)
        {
            try
            {
                _designActionError = null;
                action();
            }
            catch (Exception error) when (error is System.IO.IOException || error is System.IO.InvalidDataException
                || error is UnauthorizedAccessException || error is KeyNotFoundException
                || error is InvalidOperationException || error is ArgumentException || error is FormatException
                || error is NotSupportedException)
            {
                _designActionError = error.Message;
            }
            UpdateDesignSelection();
        }
    }
}
