using System;

namespace CapFrameX.Contracts.Configuration
{
    /// <summary>
    /// Service for resolving application paths. Supports both installed and portable modes.
    /// </summary>
    public interface IPathService
    {
        /// <summary>
        /// Gets whether the application is running in portable mode.
        /// </summary>
        bool IsPortableMode { get; }

        /// <summary>
        /// Gets the folder path for configuration files (AppSettings.json, overlay configs, etc.).
        /// </summary>
        string ConfigFolder { get; }

        /// <summary>
        /// Gets the default folder path for capture recordings.
        /// </summary>
        string CapturesFolder { get; }

        /// <summary>
        /// Gets the default folder path for screenshots.
        /// </summary>
        string ScreenshotsFolder { get; }

        /// <summary>
        /// Gets the folder path for log files.
        /// </summary>
        string LogsFolder { get; }

        /// <summary>
        /// Gets the default folder path for cloud downloads.
        /// </summary>
        string CloudFolder { get; }

        /// <summary>
        /// Resolves a relative path to an absolute path based on the current mode.
        /// In portable mode, resolves relative to the application directory.
        /// In installed mode, resolves relative to the appropriate system folder.
        /// </summary>
        /// <param name="relativePath">The relative path to resolve.</param>
        /// <returns>The absolute path.</returns>
        string ResolvePath(string relativePath);

        /// <summary>
        /// Ensures all required directories exist.
        /// </summary>
        void EnsureDirectoriesExist();

        /// <summary>
        /// Resolves a path that may contain the MyDocuments\CapFrameX placeholder.
        /// If the path contains the placeholder, returns the appropriate resolved path.
        /// Otherwise, returns the path unchanged.
        /// </summary>
        /// <param name="path">The path that may contain a placeholder.</param>
        /// <returns>The resolved path.</returns>
        string ResolveDocumentsPlaceholder(string path);
    }
}
