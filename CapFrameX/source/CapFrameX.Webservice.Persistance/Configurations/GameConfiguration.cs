using CapFrameX.Webservice.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Persistance.Configurations
{
	public class GameConfiguration : IEntityTypeConfiguration<Game>
	{

		public void Configure(EntityTypeBuilder<Game> builder)
		{
			builder.HasKey(x => x.Id);
		}
	}
}
