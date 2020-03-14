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
		public void Configure(EntityTypeBuilder<SessionProxy> builder)
		{
			builder.HasKey(x => x.Id);
			builder.Property(x => x.Session).HasConversion(
				save => JsonConvert.SerializeObject(save),
				read => JsonConvert.DeserializeObject<Session>(read)
			);
		}
	}
}
