using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Prism.Mvvm;

namespace CapFrameX.ViewModel.SubModels
{
    public sealed class ExtendedOsdLoggingViewModel : BindableBase
    {
        private readonly ExtendedOsdLoggingController _controller;
        private bool _isEnabled = false;
        private bool _isUpdating;
        private string _error = string.Empty;

        internal ExtendedOsdLoggingViewModel(ExtendedOsdLoggingController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            try
            {
                _isEnabled = _controller.IsEnabled();
            }
            catch (Exception ex)
            {
                _error = $"Extended OSD logging could not be read: {ex.Message}";
                Trace.TraceError("Failed to read extended OSD logging state: {0}", ex);
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (IsUpdating || (_isEnabled == value && !HasError))
                    return;

                _ = UpdateAsync(value);
            }
        }

        public bool IsUpdating
        {
            get => _isUpdating;
            private set => SetProperty(ref _isUpdating, value);
        }

        public string Error
        {
            get => _error;
            private set
            {
                if (SetProperty(ref _error, value))
                    RaisePropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_error);

        private async Task UpdateAsync(bool enabled)
        {
            bool previousValue = _isEnabled;
            IsUpdating = true;
            SetProperty(ref _isEnabled, enabled, nameof(IsEnabled));
            Error = string.Empty;

            try
            {
                // Registry/file writes and the Windows environment broadcast must not block WPF.
                // Await resumes on the UI context so completion/error notifications stay on it.
                await Task.Run(() => _controller.SetEnabled(enabled));
            }
            catch (Exception ex)
            {
                SetProperty(ref _isEnabled, previousValue, nameof(IsEnabled));
                Error = $"Extended OSD logging could not be updated: {ex.Message}";
                Trace.TraceError("Failed to update extended OSD logging: {0}", ex);
            }
            finally
            {
                IsUpdating = false;
            }
        }
    }
}
