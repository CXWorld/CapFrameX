using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace CapFrameX.App.Views;

public partial class MainWindow : Window
{
    public static readonly FuncValueConverter<bool, Color> ConnectionColorConverter =
        new(connected => connected ? Colors.LimeGreen : Colors.Red);

    public MainWindow()
    {
        InitializeComponent();
    }
}
