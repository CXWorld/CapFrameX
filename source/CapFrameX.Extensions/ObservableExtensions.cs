using System;
using System.Linq;
using System.Reactive.Linq;

namespace CapFrameX.Extensions
{
	public static class ObservableExtensions
	{
		public static IObservable<long> CountDown(double doubleseconds)
		{
			var seconds = Convert.ToInt32(doubleseconds);
			return Observable
				.Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1))
				.Select(i => seconds - i)
				.Take(seconds + 1);
		}
	}
}
