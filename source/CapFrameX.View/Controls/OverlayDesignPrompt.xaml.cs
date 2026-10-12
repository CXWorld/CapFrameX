using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        public string EnteredName => NameInput.Text.Trim();

        public OverlayDesignPrompt(string title, string message, string confirm,
            string initialName = null, Func<string, string> validateName = null,
            string secondary = null, bool canConfirm = true)
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
            PromptTitle.Text = title;
            PromptMessage.Text = message;
            ConfirmButton.Content = confirm;
            ConfirmButton.IsEnabled = canConfirm;
            if (secondary != null)
            {
                SecondaryButton.Content = secondary;
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
            string error = string.IsNullOrWhiteSpace(EnteredName) ? "Enter a design name." : _validateName?.Invoke(EnteredName);
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
