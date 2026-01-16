using Newtonsoft.Json;
using Serilog;
using System;
using System.IO;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Static class for detecting portable mode on application startup.
    /// Must be initialized early in the application lifecycle before DI container setup.
    /// </summary>
    public static class PortableModeDetector
    {
        private const string PortableConfigFileName = "portable.json";

        private static bool _initialized;
        private static readonly object _lock = new object();

        /// <summary>
        /// Gets whether the application is running in portable mode.
        /// </summary>
        public static bool IsPortableMode { get; private set; }

        /// <summary>
        /// Gets the portable configuration. Null if not in portable mode.
        /// </summary>
        public static PortableConfig Config { get; private set; }

        /// <summary>
        /// Gets the path to the portable.json file.
        /// </summary>
        public static string PortableConfigPath { get; private set; }

        /// <summary>
        /// Initializes portable mode detection. Should be called early in application startup.
        /// Thread-safe and idempotent - multiple calls will not re-initialize.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                PortableConfigPath = Path.Combine(appDirectory, PortableConfigFileName);

                if (File.Exists(PortableConfigPath))
                {
                    try
                    {
                        var json = File.ReadAllText(PortableConfigPath);
                        Config = JsonConvert.DeserializeObject<PortableConfig>(json);

                        if (Config != null && Config.Portable)
                        {
                            IsPortableMode = true;
                            ValidateAndNormalizePaths(Config);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log to console since logging infrastructure isn't available yet
                        Log.Error($"Warning: Failed to load portable.json: {ex.Message}");
                        Log.Error("Falling back to installed mode.");
                        IsPortableMode = false;
                        Config = null;
                    }
                }

                _initialized = true;
            }
        }

        /// <summary>
        /// Validates and normalizes paths in the portable configuration.
        /// </summary>
        private static void ValidateAndNormalizePaths(PortableConfig config)
        {
            if (config.Paths == null)
            {
                config.Paths = new PortablePathConfig();
            }

            // Ensure default paths if not specified
            if (string.IsNullOrWhiteSpace(config.Paths.Config))
                config.Paths.Config = "./Config";

            if (string.IsNullOrWhiteSpace(config.Paths.Captures))
                config.Paths.Captures = "./Captures";

            if (string.IsNullOrWhiteSpace(config.Paths.Screenshots))
                config.Paths.Screenshots = "./Screenshots";

            if (string.IsNullOrWhiteSpace(config.Paths.Logs))
                config.Paths.Logs = "./Logs";

            if (string.IsNullOrWhiteSpace(config.Paths.Cloud))
                config.Paths.Cloud = "./Captures/Cloud";
        }

        /// <summary>
        /// Creates a sample portable.json file in the application directory.
        /// Useful for creating a portable distribution.
        /// </summary>
        /// <returns>The path to the created file.</returns>
        public static string CreateSamplePortableConfig()
        {
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(appDirectory, PortableConfigFileName);

            var config = new PortableConfig
            {
                Portable = true,
                Paths = new PortablePathConfig
                {
                    Config = "./Config",
                    Captures = "./Captures",
                    Screenshots = "./Screenshots",
                    Logs = "./Logs",
                    Cloud = "./Captures/Cloud"
                }
            };

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(path, json);

            return path;
        }
    }
}
