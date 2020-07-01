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
                new ColorItem(Colors.Blue, "CX blue")
            };
        }
    }
}
