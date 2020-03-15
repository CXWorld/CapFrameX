using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
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
		[JsonConverter(typeof(ConcreteTypeConverter<SessionInfo>))]
		public ISessionInfo Info { get; set; } = new SessionInfo();
		[JsonProperty("Runs")]
		[JsonConverter(typeof(ConcreteTypeConverter<IList<SessionRun>>))]
		public IList<ISessionRun> Runs { get; set; } = new List<ISessionRun>();

		[JsonConstructor]
		public Session(List<SessionRun> runs, SessionInfo info)
		{
			Runs = new List<ISessionRun>(runs);
			Info = info;
		}

		public Session() { }
	}
}
