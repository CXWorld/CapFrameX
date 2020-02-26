using CapFrameX.Webservice.Data.Entities;
using CapFrameX.Webservice.Persistance.Configurations;
using Microsoft.EntityFrameworkCore;
using System;

namespace CapFrameX.Webservice.Persistance
{
	/// <summary>
	/// Add Migration with command in PM Console:
	/// cd source
	/// dotnet ef migrations add --project CapFrameX.Webservice.Persistance --startup-project CapFrameX.Webservice.Host
	/// 
	/// Update Database with command in PM Console:
	/// cd source
	/// dotnet ef database update --project CapFrameX.Webservice.Persistance --startup-project CapFrameX.Webservice.Host
	/// </summary>
	public class CXContext : DbContext
	{
		public DbSet<SessionCollection> SessionCollections { get; set; }

		public CXContext(DbContextOptions<CXContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.ApplyConfiguration(new SessionProxyConfiguratation());
			modelBuilder.ApplyConfiguration(new SessionCollectionConfiguration());
		}
	}
}
