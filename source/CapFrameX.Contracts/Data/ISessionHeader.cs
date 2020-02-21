using System;

namespace CapFrameX.Contracts.Data
{
	public interface ISessionHeader
	{
		Version AppVersion { get; set; }
		string Cpu { get; set; }
	}
}