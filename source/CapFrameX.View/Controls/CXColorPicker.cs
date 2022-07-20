using System.Windows.Media;
using Xceed.Wpf.Toolkit;

namespace CapFrameX.View.Controls
{
    public class CXColorPicker : ColorPicker
    {
        public CXColorPicker(): base()
        {
            StandardColors = new System.Collections.ObjectModel.ObservableCollection<ColorItem>
            {
                new ColorItem(Color.FromRgb(241, 125, 32), "CX Orange"),
                new ColorItem(Color.FromRgb(156, 210, 0), "CX Green"),
                new ColorItem(Color.FromRgb(34, 151, 243), "CX Blue"),
                new ColorItem(Color.FromRgb(255, 180, 0), "Dark Yellow"),
                new ColorItem(Color.FromRgb(200, 0, 0), "Red"),
                new ColorItem(Color.FromRgb(100, 0, 160), "Purple"),
                new ColorItem(Color.FromRgb(232, 193, 255), "Intel Pink"),
                new ColorItem(Color.FromRgb(237, 28, 36), "AMD Red"),
                new ColorItem(Color.FromRgb(0, 113, 197), "Intel Blue"),
                new ColorItem(Color.FromRgb(118, 185, 0), "Nvidia Green")
            };
        }
    }
}
