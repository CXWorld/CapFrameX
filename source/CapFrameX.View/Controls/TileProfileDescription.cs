using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using CapFrameX.Contracts.Localization;
using CapFrameX.Contracts.Overlay;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// What a shipped tile profile shows and whom it credits: its description paragraphs and links to
    /// its authors' pages. Collapsed for profiles without a description; follows the interface language.
    /// </summary>
    public sealed class TileProfileDescription : Border
    {
        public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
            nameof(Profile), typeof(OverlayDesignInfo), typeof(TileProfileDescription),
            new PropertyMetadata(null, (target, _) => ((TileProfileDescription)target).Refresh()));

        private readonly StackPanel _content = new StackPanel();
        private bool _languageSubscribed;

        public TileProfileDescription()
        {
            Child = _content;
            Visibility = Visibility.Collapsed;
            Loaded += (_, _) =>
            {
                if (!_languageSubscribed) CxLang.Instance.PropertyChanged += OnLanguageChanged;
                _languageSubscribed = true;
                Refresh();
            };
            Unloaded += (_, _) =>
            {
                if (_languageSubscribed) CxLang.Instance.PropertyChanged -= OnLanguageChanged;
                _languageSubscribed = false;
            };
        }

        public OverlayDesignInfo Profile
        {
            get => (OverlayDesignInfo)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        private void OnLanguageChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CxLang.UiLanguage)) return;
            if (Dispatcher.CheckAccess()) Refresh();
            else if (!Dispatcher.HasShutdownStarted) _ = Dispatcher.BeginInvoke(new Action(Refresh));
        }

        private void Refresh()
        {
            _content.Children.Clear();
            var profile = Profile;
            if (profile?.HasDescription != true)
            {
                Visibility = Visibility.Collapsed;
                return;
            }
            foreach (string paragraph in profile.Description)
                _content.Children.Add(new TextBlock
                {
                    Text = paragraph, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, _content.Children.Count == 0 ? 0 : 6, 0, 0)
                });
            if (profile.Links.Count > 0)
            {
                var links = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, _content.Children.Count == 0 ? 0 : 8, 0, 0) };
                foreach (var link in profile.Links)
                {
                    if (links.Inlines.Count > 0) links.Inlines.Add(new Run("  ·  "));
                    var hyperlink = new Hyperlink(new Run(link.Text)) { NavigateUri = link.Uri, ToolTip = link.Uri.AbsoluteUri };
                    hyperlink.RequestNavigate += OnRequestNavigate;
                    links.Inlines.Add(hyperlink);
                }
                _content.Children.Add(links);
            }
            Visibility = Visibility.Visible;
        }

        private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Credits only ever point to web pages; never hand another scheme to the shell.
            if (e.Uri.IsAbsoluteUri && e.Uri.Scheme == Uri.UriSchemeHttps)
                _ = Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }
    }
}
