
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace CapFrameX.ViewModel.SubModels
{
    public class ComparisonColorItems : INotifyPropertyChanged
    {
        private Color _color;
        public Color Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    RaisePropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
