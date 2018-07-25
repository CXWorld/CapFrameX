using System;

namespace SequenceGenerator
{
    public interface ISequenceGenerator<T>
    {
        IObservable<T> DataStream { get; }
        void Start();
        void Stop();
    }
}
