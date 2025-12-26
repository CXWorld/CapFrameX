using CapFrameX.Service.Data;
using Microsoft.EntityFrameworkCore;

Console.WriteLine("CapFrameX Database Tool");
Console.WriteLine("======================\n");

if (args.Length == 0 || args[0] == "help" || args[0] == "--help" || args[0] == "-h")
{
    PrintHelp();
    return;
}

var command = args[0].ToLowerInvariant();

try
{
    switch (command)
    {
        case "init":
            Console.WriteLine("Initializing database...");
            await DatabaseInitializer.InitializeDatabaseAsync();
            Console.WriteLine("\n✓ Database initialized successfully!");
            break;

        case "seed":
            Console.WriteLine("Seeding database with sample data...");
            await DatabaseInitializer.SeedSampleDataAsync();
            Console.WriteLine("\n✓ Sample data added successfully!");
            break;

        case "path":
            var dbPath = CapFrameXDbContextFactory.GetDefaultDatabasePath();
            Console.WriteLine($"Database path: {dbPath}");
            Console.WriteLine($"Exists: {File.Exists(dbPath)}");
            break;

        case "info":
            await ShowDatabaseInfo();
            break;

        case "migrate":
        case "update":
            Console.WriteLine("Applying database migrations...");
            await MigrateDatabase();
            Console.WriteLine("\n✓ Database migrations applied successfully!");
            break;

        default:
            Console.WriteLine($"Unknown command: {command}");
            Console.WriteLine("Use 'help' to see available commands.");
            break;
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"\n✗ Error: {ex.Message}");
    Console.ResetColor();
    return;
}

static void PrintHelp()
{
    Console.WriteLine("Usage: CapFrameX.DatabaseTool <command>");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  init     - Create database and apply migrations");
    Console.WriteLine("  migrate  - Apply pending migrations to existing database");
    Console.WriteLine("  update   - Alias for migrate");
    Console.WriteLine("  seed     - Add sample data (suites, sessions, runs)");
    Console.WriteLine("  path     - Show database file path");
    Console.WriteLine("  info     - Show database statistics");
    Console.WriteLine("  help     - Show this help message");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  CapFrameX.DatabaseTool init      # First time setup");
    Console.WriteLine("  CapFrameX.DatabaseTool migrate   # Apply updates");
    Console.WriteLine("  CapFrameX.DatabaseTool seed      # Add test data");
}

static async Task MigrateDatabase()
{
    var dbPath = CapFrameXDbContextFactory.GetDefaultDatabasePath();

    if (!File.Exists(dbPath))
    {
        Console.WriteLine("Database does not exist. Use 'init' command for first-time setup.");
        Environment.Exit(1);
        return;
    }

    using var context = CapFrameXDbContextFactory.Create();

    Console.WriteLine($"Database: {dbPath}");
    Console.WriteLine();

    // Check for pending migrations
    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
    var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();

    Console.WriteLine($"Applied migrations: {appliedMigrations.Count()}");
    Console.WriteLine($"Pending migrations: {pendingMigrations.Count()}");
    Console.WriteLine();

    if (!pendingMigrations.Any())
    {
        Console.WriteLine("Database is up to date. No migrations to apply.");
        return;
    }

    // Create backup before migration
    var backupPath = $"{dbPath}.backup_{DateTime.Now:yyyyMMddHHmmss}";
    Console.WriteLine($"Creating backup: {Path.GetFileName(backupPath)}");
    File.Copy(dbPath, backupPath);

    try
    {
        Console.WriteLine("Applying migrations:");
        foreach (var migration in pendingMigrations)
        {
            Console.WriteLine($"  - {migration}");
        }
        Console.WriteLine();

        await context.Database.MigrateAsync();

        Console.WriteLine("✓ Migrations applied successfully");
        Console.WriteLine($"✓ Backup saved: {backupPath}");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n✗ Migration failed: {ex.Message}");
        Console.WriteLine($"\nRestoring from backup: {backupPath}");
        Console.ResetColor();

        // Restore backup
        File.Copy(backupPath, dbPath, overwrite: true);
        Console.WriteLine("✓ Database restored from backup");
        throw;
    }
}

static async Task ShowDatabaseInfo()
{
    var dbPath = CapFrameXDbContextFactory.GetDefaultDatabasePath();

    if (!File.Exists(dbPath))
    {
        Console.WriteLine("Database does not exist yet. Run 'init' to create it.");
        return;
    }

    using var context = CapFrameXDbContextFactory.Create();

    var suiteCount = await context.Suites.CountAsync();
    var sessionCount = await context.Sessions.CountAsync();
    var runCount = await context.SessionRuns.CountAsync();

    var fileInfo = new FileInfo(dbPath);

    Console.WriteLine($"Database: {dbPath}");
    Console.WriteLine($"Size: {fileInfo.Length / 1024.0:F2} KB");
    Console.WriteLine();
    Console.WriteLine("Statistics:");
    Console.WriteLine($"  Suites:   {suiteCount,6}");
    Console.WriteLine($"  Sessions: {sessionCount,6}");
    Console.WriteLine($"  Runs:     {runCount,6}");
}
