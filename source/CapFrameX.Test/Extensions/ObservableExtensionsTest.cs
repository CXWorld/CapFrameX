using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reactive.Linq;

namespace CapFrameX.Test.Extensions
{
	[TestClass]
	public class ObservableExtensionsTest
	{
		[TestMethod]
		public void CountdownObservable_CorrectValues()
		{
			int start = 20;
			var obs = Observable
					  .Timer(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)) // timer(firstValueDelay, intervalBetweenValues)
					  .Select(i => start - i)
					  .Take(start + 1)
					  .Subscribe(t => TimerObserver((int)t));
		}		

		private void TimerObserver(int t)
		{
			int counter = 20 - t;

			Console.WriteLine("Current counter: {0}", counter);

			if (counter == 0)
			{
				Console.WriteLine("Finished");
			}
		}
	}
}
