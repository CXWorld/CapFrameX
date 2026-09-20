using CapFrameX.Service.Data;
using CapFrameX.Service.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CapFrameX.Service.Data.Tests;

/// <summary>
/// Applies the migrations to a real SQLite file, which the in-memory provider cannot stand in for:
/// it ignores relational constraints and indexes, so a migration that does not actually produce a
/// usable schema would still look fine there.
/// </summary>
public sealed class SchemaMigrationTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cfx-db-" + Guid.NewGuid().ToString("N"));

    private readonly string _databasePath;

    public SchemaMigrationTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "capframex.db");
    }

    private CapFrameXDbContext Context() =>
        new(new DbContextOptionsBuilder<CapFrameXDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options);

    [Fact]
    public async Task Migrating_a_fresh_database_creates_the_file()
    {
        await using var context = Context();

        await context.Database.MigrateAsync();

        Assert.True(File.Exists(_databasePath));
    }

    [Theory]
    [InlineData("Suites")]
    [InlineData("Sessions")]
    [InlineData("SessionRuns")]
    public async Task Migration_creates_the_expected_table(string table)
    {
        await using var context = Context();
        await context.Database.MigrateAsync();

        Assert.Contains(table, await TableNamesAsync());
    }

    [Fact]
    public async Task No_migration_is_left_pending_afterwards()
    {
        await using var context = Context();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Migrating_twice_is_harmless()
    {
        await using (var first = Context())
        {
            await first.Database.MigrateAsync();
        }

        await using var second = Context();
        await second.Database.MigrateAsync();

        Assert.Empty(await second.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task A_record_survives_a_round_trip_through_the_real_schema()
    {
        var suiteId = Guid.NewGuid();

        await using (var writer = Context())
        {
            await writer.Database.MigrateAsync();

            writer.Suites.Add(new Suite { Id = suiteId, Name = "Imported" });
            writer.Sessions.Add(new Session
            {
                SuiteId = suiteId,
                GameName = "Cyberpunk 2077",
                ProcessName = "Cyberpunk2077",
                Processor = "Ryzen 9 9950X",
                Gpu = "RTX 5090",
                Os = "Windows 11",
            });

            await writer.SaveChangesAsync();
        }

        await using var reader = Context();
        var session = await reader.Sessions.SingleAsync();

        Assert.Equal("Cyberpunk 2077", session.GameName);
        Assert.NotEqual(Guid.Empty, session.Id);
    }

    [Fact]
    public async Task A_session_cannot_exist_without_a_suite()
    {
        // The schema makes Suite ownership mandatory, so importing a capture file means creating
        // or choosing a suite for it - there is no "loose session" to fall back on. The in-memory
        // provider does not enforce this, which is why it is pinned against real SQLite.
        await using var context = Context();
        await context.Database.MigrateAsync();

        context.Sessions.Add(new Session
        {
            SuiteId = Guid.NewGuid(),
            GameName = "Orphan",
            ProcessName = "Orphan",
            Processor = "Ryzen 9 9950X",
            Gpu = "RTX 5090",
            Os = "Windows 11",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_a_suite_takes_its_sessions_with_it()
    {
        // A cascade the in-memory provider would not enforce.
        var suiteId = Guid.NewGuid();

        await using (var writer = Context())
        {
            await writer.Database.MigrateAsync();

            writer.Suites.Add(new Suite { Id = suiteId, Name = "Review" });
            writer.Sessions.Add(new Session
            {
                SuiteId = suiteId,
                GameName = "Doom",
                ProcessName = "DOOM",
                Processor = "Ryzen 9 9950X",
                Gpu = "RTX 5090",
                Os = "Windows 11",
            });

            await writer.SaveChangesAsync();
        }

        await using var context = Context();
        context.Suites.Remove(await context.Suites.SingleAsync(s => s.Id == suiteId));
        await context.SaveChangesAsync();

        Assert.Empty(await context.Sessions.ToListAsync());
    }

    private async Task<List<string>> TableNamesAsync()
    {
        var names = new List<string>();

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
