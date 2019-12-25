using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Hotkey;
using Gma.System.MouseKeyHook;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;

namespace CapFrameX.ViewModel
{
    public class OverlayViewModel : BindableBase, INavigationAware
    {
        private readonly IOverlayService _overlayService;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IEventAggregator _eventAggregator;

        private IKeyboardMouseEvents _globalOverlayHookEvent;
        private IKeyboardMouseEvents _globalResetHistoryHookEvent;

        private bool IsOverlayActive
        {
            get { return _appConfiguration.IsOverlayActive; }
            set
            {
                _appConfiguration.IsOverlayActive = value;
                _overlayService.IsOverlayActiveStream.OnNext(value);
            }
        }

        public string OverlayHotkeyString
        {
            get { return _appConfiguration.OverlayHotKey; }
            set
            {
                if (!CXHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.OverlayHotKey = value;
                UpdateGlobalOverlayHookEvent();
                RaisePropertyChanged();
            }
        }

        public string ResetHistoryHotkeyString
        {
            get { return _appConfiguration.ResetHistoryHotkey; }
            set
            {
                if (!CXHotkey.IsValidHotkey(value))
                    return;

                _appConfiguration.ResetHistoryHotkey = value;
                UpdateGlobalResetHistoryHookEvent();
                RaisePropertyChanged();
            }
        }

        public IAppConfiguration AppConfiguration => _appConfiguration;

        public OverlayViewModel(IOverlayService overlayService, IAppConfiguration appConfiguration, IEventAggregator eventAggregator)
        {
            _overlayService = overlayService;
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;

            OverlayHotkeyString = _appConfiguration.OverlayHotKey.ToString();

            if (IsOverlayActive)
                _overlayService.ShowOverlay();

            _overlayService.IsOverlayActiveStream.OnNext(_appConfiguration.IsOverlayActive);

            SetGlobalHookEventOverlayHotkey();
            SetGlobalHookEventResetHistoryHotkey();
        }

        private void UpdateGlobalOverlayHookEvent()
        {
            if (_globalOverlayHookEvent != null)
            {
                _globalOverlayHookEvent.Dispose();
                SetGlobalHookEventOverlayHotkey();
            }
        }

        private void UpdateGlobalResetHistoryHookEvent()
        {
            if (_globalResetHistoryHookEvent != null)
            {
                _globalResetHistoryHookEvent.Dispose();
                SetGlobalHookEventResetHistoryHotkey();
            }
        }

        private void SetGlobalHookEventOverlayHotkey()
        {
            if (!CXHotkey.IsValidHotkey(OverlayHotkeyString))
                return;

            var onCombinationDictionary = new Dictionary<Combination, Action>
            {
                {Combination.FromString(OverlayHotkeyString), () =>
                {
                    SetOverlayMode();
                }}
            };

            _globalOverlayHookEvent = Hook.GlobalEvents();
            _globalOverlayHookEvent.OnCombination(onCombinationDictionary);
        }

        private void SetGlobalHookEventResetHistoryHotkey()
        {
            if (!CXHotkey.IsValidHotkey(ResetHistoryHotkeyString))
                return;

            var onCombinationDictionary = new Dictionary<Combination, Action>
            {
                {Combination.FromString(ResetHistoryHotkeyString), () =>
                {
                    _overlayService.ResetHistory();
                }}
            };

            _globalResetHistoryHookEvent = Hook.GlobalEvents();
            _globalResetHistoryHookEvent.OnCombination(onCombinationDictionary);
        }

        private void SetOverlayMode()
        {
            IsOverlayActive = !IsOverlayActive;

            if (IsOverlayActive)
                _overlayService.ShowOverlay();
            else
                _overlayService.HideOverlay();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
        }
    }
}
