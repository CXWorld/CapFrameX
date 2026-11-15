using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapFrameX.Contracts.Localization;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using MaterialDesignThemes.Wpf;

namespace CapFrameX.View.Controls
{
    /// <summary>One top-level tile of the profile to merge: its place in that design and whether it is taken.</summary>
    public sealed class OverlayDesignMergeChoice : INotifyPropertyChanged
    {
        private bool _isChosen = true;
        private string _detail;

        internal OverlayDesignMergeChoice(int index, OsdDesignTile tile)
        {
            Index = index;
            Tile = tile;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public int Index { get; }
        internal OsdDesignTile Tile { get; }
        /// <summary>The tile's name as the editor's layer list shows it.</summary>
        public string Name => Tile.Label;
        /// <summary>The kind of the tile; a container also says how many elements it brings.</summary>
        public string Detail
        {
            get => _detail;
            internal set { _detail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail))); }
        }
        public bool IsChosen
        {
            get => _isChosen;
            set
            {
                if (_isChosen == value) return;
                _isChosen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChosen)));
            }
        }
    }

    /// <summary>
    /// Asks which top-level tiles of another profile join the design being edited: all of them merges the
    /// two profiles, a container alone takes just that container.
    /// </summary>
    public partial class OverlayDesignMergeDialog : UserControl
    {
        private readonly string _targetName;
        private IReadOnlyList<OverlayDesignMergeChoice> _choices = Array.Empty<OverlayDesignMergeChoice>();
        private Exception _sourceError;

        public OverlayDesignMergeDialog(IReadOnlyList<OverlayDesignProfile> sources, string targetName)
        {
            InitializeComponent();
            _targetName = targetName;
            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    DialogHost.CloseDialogCommand.Execute(OverlayDesignPromptResult.Cancel, ConfirmButton);
                    args.Handled = true;
                }
            };
            SourceSelector.ItemsSource = sources;
            SourceSelector.SelectedIndex = sources.Count > 0 ? 0 : -1;
            RefreshLanguage();
            UpdateChoice();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>The profile whose tiles are taken.</summary>
        public OverlayDesignProfile SelectedProfile
        {
            get => SourceSelector.SelectedItem as OverlayDesignProfile;
            set => SourceSelector.SelectedItem = value;
        }
        /// <summary>The top-level tiles of <see cref="SelectedProfile"/>, in layer order; all are chosen at first.</summary>
        public IReadOnlyList<OverlayDesignMergeChoice> Choices => _choices;
        /// <summary>The places of the chosen tiles among the top-level tiles of <see cref="SelectedProfile"/>.</summary>
        public IReadOnlyList<int> ChosenTiles => _choices.Where(choice => choice.IsChosen).Select(choice => choice.Index).ToArray();
        /// <summary>Whether a tile is chosen. The button also needs an open dialog host for its close command.</summary>
        public bool CanMerge { get; private set; }

        private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
        {
            _sourceError = null;
            _choices = Array.Empty<OverlayDesignMergeChoice>();
            if (SelectedProfile != null)
            {
                try
                {
                    var design = OsdDesignDocument.FromJson(SelectedProfile.DesignJson);
                    _choices = design.Tiles.Select((tile, index) => new OverlayDesignMergeChoice(index, tile)).ToArray();
                    foreach (var choice in _choices)
                    {
                        choice.PropertyChanged += (_, args) =>
                        {
                            if (args.PropertyName == nameof(OverlayDesignMergeChoice.IsChosen)) UpdateChoice();
                        };
                    }
                }
                catch (Exception error) when (error is FormatException || error is InvalidOperationException
                    || error is System.Text.Json.JsonException)
                {
                    _sourceError = error;
                }
            }
            TileChoices.ItemsSource = _choices;
            RefreshLanguage();
            UpdateChoice();
        }

        // All of them unless all are chosen already: a click on the half-checked box completes the choice.
        private void OnAllTilesClicked(object sender, RoutedEventArgs e)
        {
            bool all = _choices.Any(choice => !choice.IsChosen);
            foreach (var choice in _choices) choice.IsChosen = all;
            UpdateChoice();
        }

        // The box over the list reads all, none or (indeterminate) some; nothing chosen merges nothing.
        private void UpdateChoice()
        {
            int chosen = _choices.Count(choice => choice.IsChosen);
            AllTiles.IsEnabled = _choices.Count > 0;
            AllTiles.IsChecked = chosen == 0 ? false : chosen == _choices.Count ? true : (bool?)null;
            CanMerge = chosen > 0;
            ConfirmButton.IsEnabled = CanMerge;
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
            MergeMessage.Text = CxLang.Format("OverlayEditor_MergeMessage", _targetName);
            foreach (var choice in _choices)
            {
                string kind = OsdLocalization.ValueLabel(choice.Tile.Kind);
                int elements = CountElements(choice.Tile);
                choice.Detail = elements > 0 ? CxLang.Format("OverlayEditor_MergeContainerItem", kind, elements) : kind;
            }
            SourceError.Text = _sourceError == null ? string.Empty
                : CxLang.Format("OverlayEditor_MergeUnreadable", OverlayDesignText.ExceptionMessage(_sourceError));
            SourceError.Visibility = _sourceError == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private static int CountElements(OsdDesignTile tile) => tile.Children.Sum(child => 1 + CountElements(child));
    }
}
