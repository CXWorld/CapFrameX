using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;

namespace CapFrameX.Core.Configuration;

public interface ISettingsService : IDisposable
{
    AppSettings Settings { get; }
    IObservable<AppSettings> SettingsChanged { get; }
    void Save();
    void Load();
    void UpdateSettings(Action<AppSettings> updateAction);
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsPath;
    private readonly Subject<AppSettings> _settingsChanged = new();
    private AppSettings _settings = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings => _settings;
    public IObservable<AppSettings> SettingsChanged => _settingsChanged.AsObservable();

    public SettingsService()
    {
        // Follow XDG Base Directory Specification
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        var configDir = Path.Combine(configHome, "capframex");
        Directory.CreateDirectory(configDir);

        _settingsPath = Path.Combine(configDir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                Console.WriteLine($"[SettingsService] Loaded settings from {_settingsPath}");
            }
            else
            {
                _settings = new AppSettings();
                Save(); // Create default settings file
                Console.WriteLine($"[SettingsService] Created default settings at {_settingsPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Failed to load settings: {ex.Message}");
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Failed to save settings: {ex.Message}");
        }
    }

    public void UpdateSettings(Action<AppSettings> updateAction)
    {
        updateAction(_settings);
        Save();
        _settingsChanged.OnNext(_settings);
    }

    public void Dispose()
    {
        _settingsChanged.Dispose();
    }
}
