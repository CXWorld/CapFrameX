# CapFrameX Database Tool

Simple command-line tool to manage the CapFrameX SQLite database.

## Quick Start

```bash
# Navigate to tool directory
cd CapFrameX.Service/tools/CapFrameX.DatabaseTool

# Create database with sample data
dotnet run -- seed

# Show database info
dotnet run -- info
```

## Commands

### `init` - Create Database
Creates the database file and applies all migrations (empty database).

```bash
dotnet run -- init
```

### `seed` - Add Sample Data
Creates the database and adds sample benchmark data:
- 1 Suite (RTX 4090 Gaming Benchmark)
- 3 Sessions (Cyberpunk 2077, Red Dead Redemption 2, Starfield)
- 9 Runs (3 runs per session with realistic metrics)

```bash
dotnet run -- seed
```

### `path` - Show Database Location
Displays the database file path and whether it exists.

```bash
dotnet run -- path
```

Output:
```
Database path: C:\Users\DevTe\AppData\Local\CapFrameX\capframex.db
Exists: True
```

### `info` - Show Statistics
Displays database statistics and file size.

```bash
dotnet run -- info
```

Output:
```
Database: C:\Users\DevTe\AppData\Local\CapFrameX\capframex.db
Size: 112.00 KB

Statistics:
  Suites:        1
  Sessions:      3
  Runs:          9
```

### `help` - Show Help
Displays all available commands.

```bash
dotnet run -- help
```

## Database Location

**Default**: `%LocalAppData%\CapFrameX\capframex.db`

**Full Path**: `C:\Users\<YourUsername>\AppData\Local\CapFrameX\capframex.db`

## Inspecting the Database

Once created, you can open the database file with any SQLite tool:

### Recommended Tools

1. **DB Browser for SQLite** (Free, Visual)
   - Download: https://sqlitebrowser.org/
   - Open the `.db` file
   - Browse tables: Suites, Sessions, SessionRuns
   - Run SQL queries

2. **SQLite CLI**
   ```bash
   sqlite3 "%LOCALAPPDATA%\CapFrameX\capframex.db"
   .tables
   SELECT * FROM Suites;
   .quit
   ```

3. **VS Code Extension**
   - Install "SQLite Viewer" or "SQLite" extension
   - Right-click `.db` file → Open Database

4. **Azure Data Studio**
   - Install SQLite extension
   - Connect to database file

## Sample Data

The `seed` command creates realistic benchmark data:

**Suite**:
- Name: "RTX 4090 Gaming Benchmark"
- Type: GameReview
- Description: "Performance testing with NVIDIA RTX 4090"

**Hardware Setup**:
- CPU: AMD Ryzen 9 7950X
- Motherboard: ASUS ROG Crosshair X670E Hero
- RAM: 32GB DDR5-6000
- GPU: NVIDIA RTX 4090 (2520 MHz core, 10501 MHz memory)
- OS: Windows 11 Pro 23H2
- Driver: 537.42
- Resolution: 3840x2160 (4K)

**Games**:
1. Cyberpunk 2077 (~85 FPS average)
2. Red Dead Redemption 2 (~110 FPS average)
3. Starfield (~95 FPS average)

Each session has 3 benchmark runs with varying metrics.

## Tables Schema

### Suites
- Id (Guid)
- Name (string)
- Description (string)
- Type (enum: HardwareReview, GameReview, ComparisonSet, Miscellaneous)
- CreatedAt, UpdatedAt (DateTime)

### Sessions
- Id (Guid)
- SuiteId (Guid, foreign key)
- GameName, ProcessName
- Hardware info (Processor, GPU, RAM, etc.)
- Driver versions
- System settings (ResizableBar, HAGS, WinGameMode)
- CreatedAt (DateTime)

### SessionRuns
- Id (Guid)
- SessionId (Guid, foreign key)
- SampleTime (double)
- CaptureDataJson (string - frame timing arrays)
- SensorDataJson (string - hardware monitoring)
- Pre-computed metrics (AverageFps, P1Fps, P99Fps, etc.)
- Hardware metrics (AvgCpuTemp, AvgGpuTemp, etc.)
- CreatedAt (DateTime)

## Development

Build the tool:
```bash
dotnet build
```

Run with arguments:
```bash
dotnet run -- <command>
```

## Notes

- The `seed` command checks if data already exists and skips seeding if the database isn't empty
- Database is automatically created in the default location if it doesn't exist
- All timestamps are stored in UTC
- JSON columns preserve complete raw capture and sensor data
- Pre-computed metrics enable fast queries without parsing JSON
