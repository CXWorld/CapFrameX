using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Statistics.NetStandard.Contracts
{
	public interface IFrametimeStatisticProviderOptions
	{
		int MovingAverageWindowSize { get; set; }
		int FpsValuesRoundingDigits { get; set; }
	}
}
