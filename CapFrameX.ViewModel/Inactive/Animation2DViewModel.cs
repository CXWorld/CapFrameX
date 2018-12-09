using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CapFrameX.ViewModel
{
    public class Animation2DViewModel : BindableBase
    {
        private int _stepWidth;
        private int _refreshRate;
        private int _xPos;
        private IDisposable _disposableSequence;
        private Canvas _frameTestCanvas;
        private int _fpsCounter;

        public int FpsCounter
        {
            get { return _fpsCounter; }
            set { _fpsCounter = value; RaisePropertyChanged(); }
        }

        public ICommand StartAnimationCommand { get; }

        public ICommand StopAnimationCommand { get; }

        public ICommand RefreshRateChangedCommand { get; }

        public ICommand StepWidthChangedCommand { get; }

        public Animation2DViewModel(Canvas frameTestCanvas)
        {
            _frameTestCanvas = frameTestCanvas;

            StartAnimationCommand = new DelegateCommand(OnStartAnimation);
            StopAnimationCommand = new DelegateCommand(OnStopAnimation);
            RefreshRateChangedCommand = new DelegateCommand<object>((x) => OnRefreshRateChanged(x));
            StepWidthChangedCommand = new DelegateCommand<object>((x) => OnStepWidthChanged(x));

            FpsCounter = (int)Math.Round(1000d / 2, 0);
        }

        private void OnStartAnimation()
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
                                //.ObserveOn(Scheduler.Default) //no heavy background workload
                                .ObserveOn(context)
                                .SubscribeOn(context)
                                .Subscribe(x => UpdateRectangle());
        }

        private void OnStopAnimation()
        {
            _disposableSequence?.Dispose();
            _frameTestCanvas.Children.Clear();
        }

        private void OnRefreshRateChanged(object value)
        {
            _refreshRate = Convert.ToInt32(value);
            FpsCounter = (int)Math.Round(1000d / _refreshRate, 0);
        }

        private void OnStepWidthChanged(object value)
        {
            _stepWidth = Convert.ToInt32(value);
        }

        private void UpdateRectangle()
        {
            _frameTestCanvas.Children.Clear();

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

            _frameTestCanvas.Children.Add(r);

            _xPos += _stepWidth;

            if (_xPos > _frameTestCanvas.ActualWidth - 120)
                _xPos = 100;
        }
    }
}
