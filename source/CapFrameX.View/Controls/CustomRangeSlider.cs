using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Media;

namespace CapFrameX.View.Controls
{
    public class CustomRangeSlider: RangeSlider
    {
        public Brush TrackColor
        {
            get { return (Brush)GetValue(TrackColorProperty); }
            set { SetValue(TrackColorProperty, value); }
        }

        public static readonly DependencyProperty TrackColorProperty =
                DependencyProperty.Register("TrackColor", typeof(Brush), typeof(CustomRangeSlider), new PropertyMetadata(null));
    }
}
