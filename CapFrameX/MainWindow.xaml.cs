using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CapFrameX
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _tokenSource;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click_Start(object sender, RoutedEventArgs e)
        {
            _tokenSource = new CancellationTokenSource();
            CancellationToken token = _tokenSource.Token;

            int xPos = 100;

            // Going to make this better with Observables later 
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    Point p = new Point(xPos, 100);

                    // Update the UI on the UI thread
                    Dispatcher.Invoke(() =>
                    {
                        // Initialize a new Rectangle
                        Rectangle r = new Rectangle
                        {
                            // Set up rectangle's size
                            Width = 100,
                            Height = 300,

                            // Set up the Background color
                            Fill = Brushes.Black
                        };

                        // Set up the position in the canvas control
                        Canvas.SetTop(r, p.Y);
                        Canvas.SetLeft(r, p.X);

                        FrameTestCanvas.Children.Add(r);
                    });

                    // Refresh rate
                    await Task.Delay(6);

                    // Update the UI on the UI thread
                    Dispatcher.Invoke(() => FrameTestCanvas.Children.Clear());

                    // Refresh rate and this parameter defines velocity of the sliding object
                    xPos += 2;

                    if (xPos > FrameTestCanvas.ActualWidth - 100)
                        xPos = 100;
                }
            });
        }

        private void Button_Click_End(object sender, RoutedEventArgs e)
        {
            _tokenSource?.Cancel();
            FrameTestCanvas.Children.Clear();
        }
    }
}
