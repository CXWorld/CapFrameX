using Newtonsoft.Json;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Configuration model for portable mode settings.
    /// Loaded from portable.json in the application directory.
    /// </summary>
    public class PortableConfig
    {
        /// <summary>
        /// Indicates whether portable mode is enabled.
        /// </summary>
        [JsonProperty("portable")]
        public bool Portable { get; set; } = true;

        /// <summary>
        /// Path configuration for portable mode.
        /// </summary>
        [JsonProperty("paths")]
        public PortablePathConfig Paths { get; set; } = new PortablePathConfig();
    }

    /// <summary>
    /// Path configuration within portable.json.
    /// All paths are relative to the application directory.
    /// </summary>
    public class PortablePathConfig
    {
        /// <summary>
        /// Relative path for configuration files (AppSettings.json, overlay configs, sensor config).
        /// Default: "./Config"
        /// </summary>
        [JsonProperty("config")]
        public string Config { get; set; } = "./Config";

        /// <summary>
        /// Relative path for capture recordings.
        /// Default: "./Captures"
        /// </summary>
        [JsonProperty("captures")]
        public string Captures { get; set; } = "./Captures";

        /// <summary>
        /// Relative path for screenshots.
        /// Default: "./Screenshots"
        /// </summary>
        [JsonProperty("screenshots")]
        public string Screenshots { get; set; } = "./Screenshots";

        /// <summary>
        /// Relative path for log files.
        /// Default: "./Logs"
        /// </summary>
        [JsonProperty("logs")]
        public string Logs { get; set; } = "./Logs";

        /// <summary>
        /// Relative path for cloud downloads.
        /// Default: "./Captures/Cloud"
        /// </summary>
        [JsonProperty("cloud")]
        public string Cloud { get; set; } = "./Captures/Cloud";
    }
}
