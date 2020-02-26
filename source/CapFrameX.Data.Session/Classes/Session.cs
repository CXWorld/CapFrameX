using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CapFrameX.Data.Session.Classes
{
	public sealed class Session : ISession
	{
		public string Hash { get; set; }
		[JsonProperty("Info")]
		public ISessionInfo Info { get; set; } = new SessionInfo();
		[JsonProperty("Runs")]
		public IList<ISessionRun> Runs { get; set; } = new List<ISessionRun>();
	}
}
