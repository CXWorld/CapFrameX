using CapFrameX.Service.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data;

/// <summary>
/// Entity Framework Core database context for CapFrameX.
/// Manages benchmark suites, sessions, and runs with SQLite storage.
/// </summary>
public class CapFrameXDbContext : DbContext
{
    public CapFrameXDbContext(DbContextOptions<CapFrameXDbContext> options)
        : base(options)
    {
    }

    public DbSet<Suite> Suites => Set<Suite>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionRun> SessionRuns => Set<SessionRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Suite
        modelBuilder.Entity<Suite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();

            // Index for common queries
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Type, e.CreatedAt });

            // One-to-many relationship with Sessions
            entity.HasMany(e => e.Sessions)
                .WithOne(e => e.Suite)
                .HasForeignKey(e => e.SuiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Session
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Hash).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.GameName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ProcessName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.Processor).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Motherboard).HasMaxLength(200);
            entity.Property(e => e.SystemRam).HasMaxLength(100);
            entity.Property(e => e.Gpu).IsRequired().HasMaxLength(200);
            entity.Property(e => e.BaseDriverVersion).HasMaxLength(50);
            entity.Property(e => e.DriverPackage).HasMaxLength(100);
            entity.Property(e => e.GpuDriverVersion).HasMaxLength(50);
            entity.Property(e => e.Os).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ApiInfo).HasMaxLength(50);
            entity.Property(e => e.PresentationMode).HasMaxLength(50);
            entity.Property(e => e.ResolutionInfo).HasMaxLength(50);

            // Indexes for REST API queries
            entity.HasIndex(e => e.GameName);
            entity.HasIndex(e => e.ProcessName);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Processor);
            entity.HasIndex(e => e.Gpu);
            entity.HasIndex(e => new { e.GameName, e.CreatedAt });
            entity.HasIndex(e => new { e.SuiteId, e.CreatedAt });

            // One-to-many relationship with SessionRuns
            entity.HasMany(e => e.Runs)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SessionRun
        modelBuilder.Entity<SessionRun>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Hash).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.PresentMonRuntime).HasMaxLength(50);
            entity.Property(e => e.SampleTime).IsRequired();

            // JSON columns for large data arrays
            entity.Property(e => e.CaptureDataJson).IsRequired();
            entity.Property(e => e.SensorDataJson).IsRequired();
            entity.Property(e => e.RtssFrameTimesJson);
            entity.Property(e => e.PmdGpuPowerJson);
            entity.Property(e => e.PmdCpuPowerJson);
            entity.Property(e => e.PmdSystemPowerJson);

            // Indexes for metric queries (REST API filtering/sorting)
            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.AverageFps);
            entity.HasIndex(e => e.P1Fps);
            entity.HasIndex(e => e.P99Fps);
            entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
        });
    }
}
