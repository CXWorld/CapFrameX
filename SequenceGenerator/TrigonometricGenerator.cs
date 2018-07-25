using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SequenceGenerator
{
    public class TrigonometricGenerator : ISequenceGenerator<int>
    {
        private bool _run;
        private double _stepWidth;
        private double _timeSpan;

        public IObservable<int> DataStream { get; private set; }

        public void Start()
        {
            _run = true;
        }

        public void Stop()
        {
            _run = false;
        }

        public TrigonometricGenerator()
        {
            _timeSpan = 10;
            _stepWidth = 0.01;
            DataStream = InitInnerObservable();
        }

        private IObservable<int> InitInnerObservable()
        {
            return Observable.Interval(TimeSpan.FromMilliseconds(_timeSpan), new EventLoopScheduler())
                             .Where(s => _run)
                             .Scan(0d, (acc, source) => (acc + _stepWidth))
                             .Select(x => (int)Math.Sin(x));
        }
    }
}
