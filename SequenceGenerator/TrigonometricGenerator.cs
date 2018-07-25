using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace SequenceGenerator
{
    public class TrigonometricGenerator : ISequenceGenerator<int>
    {
        private bool _run;

        public IObservable<int> DataStream { get; private set; }

        public void Start()
        {
            _run = true;
        }

        public void Stop()
        {
            _run = false;
        }

        private IObservable<int> InitInnerObservable()
        {
            return Observable.Interval(TimeSpan.FromMilliseconds(10), new EventLoopScheduler())
                             .Where(s => _run)
                             .Scan(0d, (acc, source) => (acc + 0.01))
                             .Select(x => (int)Math.Sin(x));
        }
    }
}
