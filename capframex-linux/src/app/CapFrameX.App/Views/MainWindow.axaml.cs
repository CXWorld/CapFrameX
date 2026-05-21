using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace CapFrameX.App.Views;

public partial class MainWindow : Window
{
    private static readonly IBrush ConnectedBrush = new SolidColorBrush(Color.Parse("#4AA3DF"));
    private static readonly IBrush DisconnectedBrush = new SolidColorBrush(Color.Parse("#666666"));

    public static readonly FuncValueConverter<bool?, IBrush> ConnectionBrushConverter =
        new(connected => connected == true ? ConnectedBrush : DisconnectedBrush);

    public static readonly IValueConverter TabIndexConverter = new TabIndexToVisibilityConverter();

    public MainWindow()
    {
        InitializeComponent();
        Icon = null;
    }
}

public class TabIndexToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out var targetIndex))
            return index == targetIndex;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
