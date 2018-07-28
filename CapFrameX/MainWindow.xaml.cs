using System;
using System.Reactive.Linq;
using System.Threading;
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
        private int _stepWidth;
        private int _refreshRate;
        private int _xPos;
        private IDisposable _disposableSequence;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click_Start(object sender, RoutedEventArgs e)
        {
            _stepWidth = 2;
            _refreshRate = 6;
            _xPos = 2;

            var context = SynchronizationContext.Current;

            _disposableSequence?.Dispose();
            _disposableSequence = Observable.Generate(
                                    0, // initialState
                                    x => true, //condition
                                    x => x, //iterate
                                    x => x, //resultSelector
                                    x => TimeSpan.FromMilliseconds(_refreshRate))
                                //.ObserveOn(Scheduler.Default)
                                .ObserveOn(context)
                                .SubscribeOn(context)
                                .Subscribe(x => UpdateRectangle());
        }

        private void UpdateRectangle()
        {
            FrameTestCanvas.Children.Clear();

            Point p = new Point(_xPos, 100);

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

            _xPos += _stepWidth;

            if (_xPos > FrameTestCanvas.ActualWidth - 120)
                _xPos = 100;
        }

        private void Button_Click_End(object sender, RoutedEventArgs e)
        {
            _disposableSequence?.Dispose();
            FrameTestCanvas.Children.Clear();
        }

        private void Slider_Stepwidth_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;
            _stepWidth = (int)slider.Value;
        }

        private void Slider_Refreshrate_ValueChanged(object sender,
          RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;
            _refreshRate = (int)slider.Value;
            FpsCounterTextBox.Text = Math.Round(1000d / _refreshRate, 0).ToString();
        }
    }
}
