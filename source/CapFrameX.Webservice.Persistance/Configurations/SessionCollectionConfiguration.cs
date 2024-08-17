using CapFrameX.Webservice.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Persistance.Configurations
{
	class SessionCollectionConfiguration : IEntityTypeConfiguration<SessionCollection>
	{
		public void Configure(EntityTypeBuilder<SessionCollection> builder)
		{
			builder.HasKey(x => x.Id);
			builder.Property(x => x.Timestamp).ValueGeneratedOnAdd();
			builder.HasMany(x => x.Sessions).WithOne(x => x.SessionCollection).HasForeignKey(x => x.SessionCollectionId);
		}
	}
}
