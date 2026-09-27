using System;
using System.Globalization;
using System.Windows.Input;

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

        public string CaptureTimeScopeLabel => HasGameCaptureTime ? "This game" : "Global";

        public string CaptureTimeScopeToolTip => HasGameCaptureTime
            ? $"Capture duration saved for {_currentGameNameToCapture} ({_currentProcessToCapture}). Click to use the global duration."
            : "Global capture duration. Click to remember a separate duration for the selected game.";

        public string CaptureTimeProcessLabel => string.IsNullOrWhiteSpace(_currentProcessToCapture)
            ? "Global capture duration" : _currentProcessToCapture;

        public string GlobalCaptureTimeDescription =>
            (_appConfiguration.CaptureTime == 0 ? "No limit" : _appConfiguration.CaptureTime.ToString(CultureInfo.InvariantCulture) + " s")
            + (HasGameCaptureTime ? " · Removes this game's custom duration" : " · Used by games without a custom duration");

        public string GameCaptureTimeDescription => string.IsNullOrWhiteSpace(_currentProcessToCapture)
            ? "Select a process first" : $"Changes apply to {_currentGameNameToCapture} only";

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
