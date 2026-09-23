using System;
using System.Globalization;
using System.Windows.Input;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.ViewModel
{
    public partial class CaptureViewModel
    {
        private bool _isCaptureTimeEdited;
        private bool _hasGameCaptureTime;

        // This is an editor buffer. Loading a game's duration must never write to the
        // global configuration, and partial input must not be saved on every keystroke.
        public string CaptureTimeString
        {
            get => _captureTimeString;
            set
            {
                if (SetProperty(ref _captureTimeString, value))
                    _isCaptureTimeEdited = true;
            }
        }

        public bool HasGameCaptureTime => _hasGameCaptureTime;

        public bool UseGlobalCaptureTime => !HasGameCaptureTime;

        public bool CanRememberGameCaptureTime => AreButtonsActive && !string.IsNullOrWhiteSpace(_currentProcessToCapture);

        public string CaptureTimeScopeLabel => HasGameCaptureTime
            ? CxLang.T("CaptureViewModel_ThisGame")
            : CxLang.T("CaptureViewModel_Global");

        public string CaptureTimeScopeToolTip => HasGameCaptureTime
            ? string.Format(CxLang.T("CaptureViewModel_CaptureDurationSavedFor"), _currentGameNameToCapture, _currentProcessToCapture)
            : CxLang.T("CaptureViewModel_GlobalCaptureDurationClick");

        public string CaptureTimeProcessLabel => string.IsNullOrWhiteSpace(_currentProcessToCapture)
            ? CxLang.T("CaptureViewModel_GlobalCaptureDuration") : _currentProcessToCapture;

        public string GlobalCaptureTimeDescription =>
            (_appConfiguration.CaptureTime == 0 ? CxLang.T("CaptureViewModel_NoLimit") : _appConfiguration.CaptureTime.ToString(CultureInfo.InvariantCulture) + " s")
            + (HasGameCaptureTime ? CxLang.T("CaptureViewModel_RemovesThisGamesCustom") : CxLang.T("CaptureViewModel_UsedByGamesWithoutA"));

        public string GameCaptureTimeDescription => string.IsNullOrWhiteSpace(_currentProcessToCapture)
            ? CxLang.T("CaptureViewModel_SelectAProcessFirst")
            : string.Format(CxLang.T("CaptureViewModel_ChangesApplyTo0Only"), _currentGameNameToCapture);

        public ICommand UseGlobalCaptureTimeCommand { get; }

        public ICommand RememberGameCaptureTimeCommand { get; }

        public bool CommitCaptureTime()
        {
            if (!AreButtonsActive)
                return false;

            if (!double.TryParse(CaptureTimeString?.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture, out double duration) || !double.IsFinite(duration) || duration < 0)
                return false;

            if (_isCaptureTimeEdited)
            {
                if (HasGameCaptureTime)
                    SetGameCaptureTime(duration);
                else
                    _appConfiguration.CaptureTime = duration;
            }

            RestoreCaptureTime();
            return true;
        }

        public void RestoreCaptureTime()
        {
            _isCaptureTimeEdited = false;
            _hasGameCaptureTime = GetGameCaptureTime(_currentProcessToCapture).HasValue;
            SetProperty(ref _captureTimeString,
                GetEffectiveCaptureTime(_currentProcessToCapture).ToString(CultureInfo.InvariantCulture), nameof(CaptureTimeString));
            RaisePropertyChanged(nameof(HasGameCaptureTime));
            RaisePropertyChanged(nameof(UseGlobalCaptureTime));
            RaisePropertyChanged(nameof(CanRememberGameCaptureTime));
            RaisePropertyChanged(nameof(CaptureTimeScopeLabel));
            RaisePropertyChanged(nameof(CaptureTimeScopeToolTip));
            RaisePropertyChanged(nameof(CaptureTimeProcessLabel));
            RaisePropertyChanged(nameof(GlobalCaptureTimeDescription));
            RaisePropertyChanged(nameof(GameCaptureTimeDescription));
        }

        private double? GetGameCaptureTime(string process)
        {
            if (string.IsNullOrWhiteSpace(process))
                return null;

            var duration = _processList.FindProcessByName(process)?.LastCaptureTime;
            return duration.HasValue && double.IsFinite(duration.Value) && duration.Value >= 0 ? duration : null;
        }

        private double GetEffectiveCaptureTime(string process) => GetGameCaptureTime(process) ?? _appConfiguration.CaptureTime;

        private void UseGlobalCaptureDuration()
        {
            if (!AreButtonsActive)
                return;

            SetGameCaptureTime(null);
            RestoreCaptureTime();
        }

        private void RememberGameCaptureDuration()
        {
            if (!CanRememberGameCaptureTime || !CommitCaptureTime())
                return;

            if (!HasGameCaptureTime)
                SetGameCaptureTime(_appConfiguration.CaptureTime);

            RestoreCaptureTime();
        }

        private void SetGameCaptureTime(double? duration)
        {
            if (string.IsNullOrWhiteSpace(_currentProcessToCapture))
                return;

            var entry = _processList.FindProcessByName(_currentProcessToCapture);
            if (entry == null)
            {
                if (!duration.HasValue)
                    return;
                _processList.AddEntry(_currentProcessToCapture, null, false, duration);
            }
            else
            {
                if (entry.LastCaptureTime == duration)
                    return;
                entry.UpdateCaptureTime(duration);
            }

            _ = _processList.Save();
        }
    }
}
