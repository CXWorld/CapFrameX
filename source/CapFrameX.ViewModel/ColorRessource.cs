using OxyPlot;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
    public static class ColorRessource
    {
        public readonly static SolidColorBrush PieChartSmmoothFill = new SolidColorBrush(Color.FromRgb(34, 151, 243));

        public readonly static SolidColorBrush PieChartStutterFill = Brushes.Red;

        public readonly static SolidColorBrush LShapeStroke = new SolidColorBrush(Color.FromRgb(156, 210, 0));

        public readonly static SolidColorBrush BarChartFill = new SolidColorBrush(Color.FromRgb(241, 125, 32));
    }
}
