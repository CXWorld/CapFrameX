using Prism.Mvvm;

namespace CapFrameX.ViewModel.SubModels
{
    /// <summary>
    /// One entry of the comparison sort combo box. <see cref="Id"/> is what the sorting code
    /// compares against and never changes; only <see cref="DisplayName"/> follows the UI
    /// language, so a language switch keeps the current selection.
    /// </summary>
    public class SortMetricOption : BindableBase
    {
        private string _displayName;

        public SortMetricOption(string id, string displayName)
        {
            Id = id;
            _displayName = displayName;
        }

        public string Id { get; }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }
    }
}
