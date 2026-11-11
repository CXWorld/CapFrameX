using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapFrameX.Contracts.Localization;
using MaterialDesignThemes.Wpf;

namespace CapFrameX.View.Controls
{
    public enum OverlayDesignPromptResult
    {
        Cancel,
        Confirm,
        Secondary
    }

    public partial class OverlayDesignPrompt : UserControl
    {
        private readonly Func<string, string> _validateName;
        private readonly Func<string> _title;
        private readonly Func<string> _message;
        private readonly Func<string> _confirm;
        private readonly Func<string> _secondary;
        public string EnteredName => NameInput.Text.Trim();

        public OverlayDesignPrompt(string title, string message, string confirm,
            string initialName = null, Func<string, string> validateName = null,
            string secondary = null, bool canConfirm = true)
            : this(() => title, () => message, () => confirm, initialName, validateName,
                  secondary == null ? null : () => secondary, canConfirm)
        {
        }

        public OverlayDesignPrompt(Func<string> title, Func<string> message, Func<string> confirm,
            string initialName = null, Func<string, string> validateName = null,
            Func<string> secondary = null, bool canConfirm = true)
        {
            InitializeComponent();
            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    DialogHost.CloseDialogCommand.Execute(OverlayDesignPromptResult.Cancel, ConfirmButton);
                    args.Handled = true;
                }
            };
            _validateName = validateName;
            _title = title;
            _message = message;
            _confirm = confirm;
            _secondary = secondary;
            ConfirmButton.IsEnabled = canConfirm;
            if (secondary != null)
            {
                SecondaryButton.Visibility = Visibility.Visible;
            }
            if (initialName != null)
            {
                NameInput.Visibility = Visibility.Visible;
                NameInput.Text = initialName;
                Loaded += (sender, args) =>
                {
                    NameInput.Focus();
                    NameInput.SelectAll();
                };
                ValidateName();
            }
            RefreshLanguage();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CxLang.Instance.PropertyChanged -= OnLanguageChanged;
            CxLang.Instance.PropertyChanged += OnLanguageChanged;
            RefreshLanguage();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => CxLang.Instance.PropertyChanged -= OnLanguageChanged;

        private void OnLanguageChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CxLang.UiLanguage)) return;
            if (Dispatcher.CheckAccess()) RefreshLanguage();
            else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                _ = Dispatcher.BeginInvoke(new Action(() => { if (IsLoaded) RefreshLanguage(); }));
        }

        private void RefreshLanguage()
        {
            PromptTitle.Text = _title();
            PromptMessage.Text = _message();
            ConfirmButton.Content = _confirm();
            if (_secondary != null) SecondaryButton.Content = _secondary();
            if (NameInput.Visibility == Visibility.Visible) ValidateName();
        }

        private void OnNameChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmButton != null && NameInput.Visibility == Visibility.Visible)
            {
                ValidateName();
            }
        }

        private void ValidateName()
        {
            string error = string.IsNullOrWhiteSpace(EnteredName) ? CxLang.T("OverlayEditor_EnterName") : _validateName?.Invoke(EnteredName);
            NameError.Text = error;
            NameError.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
            ConfirmButton.IsEnabled = error == null;
        }

        private void OnNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ConfirmButton.IsEnabled)
            {
                DialogHost.CloseDialogCommand.Execute(OverlayDesignPromptResult.Confirm, ConfirmButton);
                e.Handled = true;
            }
        }
    }
}
