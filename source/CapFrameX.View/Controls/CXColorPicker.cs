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
                new ColorItem(Color.FromRgb(118, 185, 0), "Nvidia Green"),
                new ColorItem(Color.FromRgb(237, 28, 36), "AMD Red"),
                new ColorItem(Color.FromRgb(0, 113, 197), "Intel Blue"),
                new ColorItem(Color.FromRgb(241, 125, 32), "CX Orange"),
                new ColorItem(Color.FromRgb(255, 180, 0), "Dark Yellow"),
                new ColorItem(Color.FromRgb(100, 0, 160), "Purple"),
                new ColorItem(Color.FromRgb(152, 101, 235), "Intel Arc"),
                new ColorItem(Color.FromRgb(0, 255, 255), "Cyan"),
                new ColorItem(Color.FromRgb(160, 82, 45), "Brown"),
                new ColorItem(Color.FromRgb(0, 0, 205), "Dark Blue"),   
            };
        }
    }
}
