using CapFrameX.Webservice.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Persistance.Configurations
{
	public class IgnoreEntryConfiguration : IEntityTypeConfiguration<IgnoreEntry>
	{

		public void Configure(EntityTypeBuilder<IgnoreEntry> builder)
		{
			builder.HasKey(x => x.Id);
		}
	}
}
