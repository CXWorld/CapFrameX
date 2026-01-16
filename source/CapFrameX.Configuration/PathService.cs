using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Service for resolving application paths. Supports both installed and portable modes.
    /// </summary>
    public class PathService : IPathService
    {
        private readonly ILogger<PathService> _logger;
        private readonly string _appDirectory;

        public bool IsPortableMode { get; }
        public string ConfigFolder { get; }
        public string CapturesFolder { get; }
        public string ScreenshotsFolder { get; }
        public string LogsFolder { get; }
        public string CloudFolder { get; }

        public PathService(ILogger<PathService> logger)
        {
            _logger = logger;
            _appDirectory = AppDomain.CurrentDomain.BaseDirectory;

            var portableConfig = PortableModeDetector.Config;
            IsPortableMode = PortableModeDetector.IsPortableMode;

            if (IsPortableMode && portableConfig != null)
            {
                _logger.LogInformation("Running in portable mode");

                ConfigFolder = ResolvePath(portableConfig.Paths.Config);
                CapturesFolder = ResolvePath(portableConfig.Paths.Captures);
                ScreenshotsFolder = ResolvePath(portableConfig.Paths.Screenshots);
                LogsFolder = ResolvePath(portableConfig.Paths.Logs);
                CloudFolder = ResolvePath(portableConfig.Paths.Cloud);
            }
            else
            {
                _logger.LogInformation("Running in installed mode");

                // Standard installed mode paths
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                ConfigFolder = Path.Combine(appDataPath, "CapFrameX", "Configuration");
                CapturesFolder = Path.Combine(documentsPath, "CapFrameX", "Captures");
                ScreenshotsFolder = Path.Combine(documentsPath, "CapFrameX", "Screenshots");
                LogsFolder = Path.Combine(appDataPath, "CapFrameX", "Logs");
                CloudFolder = Path.Combine(documentsPath, "CapFrameX", "Captures", "Cloud");
            }

            _logger.LogInformation("ConfigFolder: {ConfigFolder}", ConfigFolder);
            _logger.LogInformation("CapturesFolder: {CapturesFolder}", CapturesFolder);
            _logger.LogInformation("ScreenshotsFolder: {ScreenshotsFolder}", ScreenshotsFolder);
            _logger.LogInformation("LogsFolder: {LogsFolder}", LogsFolder);
            _logger.LogInformation("CloudFolder: {CloudFolder}", CloudFolder);
        }

        public string ResolvePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return _appDirectory;

            // Handle paths starting with "./" or ".\\"
            if (relativePath.StartsWith("./") || relativePath.StartsWith(".\\"))
            {
                relativePath = relativePath.Substring(2);
            }

            // Combine with app directory and normalize
            var fullPath = Path.Combine(_appDirectory, relativePath);
            return Path.GetFullPath(fullPath);
        }

        public void EnsureDirectoriesExist()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                    _logger.LogInformation("Created directory: {ConfigFolder}", ConfigFolder);
                }

                if (!Directory.Exists(CapturesFolder))
                {
                    Directory.CreateDirectory(CapturesFolder);
                    _logger.LogInformation("Created directory: {CapturesFolder}", CapturesFolder);
                }

                if (!Directory.Exists(ScreenshotsFolder))
                {
                    Directory.CreateDirectory(ScreenshotsFolder);
                    _logger.LogInformation("Created directory: {ScreenshotsFolder}", ScreenshotsFolder);
                }

                if (!Directory.Exists(LogsFolder))
                {
                    Directory.CreateDirectory(LogsFolder);
                    _logger.LogInformation("Created directory: {LogsFolder}", LogsFolder);
                }

                if (!Directory.Exists(CloudFolder))
                {
                    Directory.CreateDirectory(CloudFolder);
                    _logger.LogInformation("Created directory: {CloudFolder}", CloudFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create required directories");
                throw;
            }
        }

        public string ResolveDocumentsPlaceholder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Check for Cloud subfolder first (more specific match)
            if (path.Contains(@"MyDocuments\CapFrameX\Captures\Cloud"))
            {
                return CloudFolder;
            }

            // Check for Captures folder
            if (path.Contains(@"MyDocuments\CapFrameX\Captures"))
            {
                return CapturesFolder;
            }

            // Check for Screenshots folder
            if (path.Contains(@"MyDocuments\CapFrameX\Screenshots"))
            {
                return ScreenshotsFolder;
            }

            // No placeholder found, return path as-is
            return path;
        }
    }
}
