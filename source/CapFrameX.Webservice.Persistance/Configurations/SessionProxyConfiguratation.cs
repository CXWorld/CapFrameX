using CapFrameX.Data.Session.Classes;
using CapFrameX.Webservice.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Persistance.Configurations
{
	class SessionProxyConfiguratation : IEntityTypeConfiguration<SessionProxy>
	{
		private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings()
		{
			TypeNameHandling = TypeNameHandling.Auto
		};
		public void Configure(EntityTypeBuilder<SessionProxy> builder)
		{
			builder.HasKey(x => x.Id);
			builder.Property(x => x.Session).HasConversion(
				save => JsonConvert.SerializeObject(save, _jsonSettings),
				read => JsonConvert.DeserializeObject<Session>(read, _jsonSettings)
			);
		}
	}
}
