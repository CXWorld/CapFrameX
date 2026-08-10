using System.Reactive.Concurrency;
using System.Windows.Threading;

namespace System.Reactive.Linq
{
	// System.Reactive 6.x only ships its WPF dispatcher operators in the
	// windows10.0.19041 target framework assets. This shim restores the classic
	// ObserveOnDispatcher/SubscribeOnDispatcher operators for plain
	// net9.0-windows targets without widening the TFM to the Windows SDK.
	public static class DispatcherObservableExtensions
	{
		public static IObservable<TSource> ObserveOnDispatcher<TSource>(this IObservable<TSource> source)
		{
			var context = new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher);
			return source.ObserveOn(new SynchronizationContextScheduler(context));
		}

		public static IObservable<TSource> SubscribeOnDispatcher<TSource>(this IObservable<TSource> source)
		{
			var context = new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher);
			return source.SubscribeOn(new SynchronizationContextScheduler(context));
		}
	}
}
