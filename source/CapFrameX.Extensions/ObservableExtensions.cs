using System;
using System.Linq;
using System.Reactive.Linq;

namespace CapFrameX.Extensions
{
    public static class ObservableExtensions
    {
        public static IObservable<long> CountDown(double seconds)
        {
            return Observable
                .Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
                .Select(i => (int)seconds - i)
                .Take((int)seconds + 1);
        }
    }
}
