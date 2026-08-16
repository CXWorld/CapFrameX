using System;
using System.Reactive.Linq;

namespace CapFrameX.Sensor
{
    internal static class SensorStreamGate
    {
        public static IObservable<T> WhileActive<T>(
            IObservable<bool> activityStream,
            Func<IObservable<T>> sourceFactory,
            Func<T> inactiveValueFactory)
        {
            if (activityStream == null)
                throw new ArgumentNullException(nameof(activityStream));
            if (sourceFactory == null)
                throw new ArgumentNullException(nameof(sourceFactory));
            if (inactiveValueFactory == null)
                throw new ArgumentNullException(nameof(inactiveValueFactory));

            return activityStream
                .DistinctUntilChanged()
                .Select(isActive => isActive
                    ? Observable.Defer(sourceFactory)
                    : Observable.Return(inactiveValueFactory()))
                .Switch();
        }
    }
}
