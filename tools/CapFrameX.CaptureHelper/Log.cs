using System;
using System.IO;

namespace CapFrameX.CaptureHelper
{
    /// <summary>
    /// Appends to CaptureHelper.log in the settings folder. Nobody tests on Windows 7 before a user
    /// does, so PresentMon's arguments, its stderr and every capture end up here.
    /// </summary>
    internal static class Log
    {
        private const long MaximumSize = 1024 * 1024;
        private static readonly object Gate = new object();

        public static string FilePath => Path.Combine(Settings.ConfigurationFolder, "CaptureHelper.log");

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Settings.ConfigurationFolder);
                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > MaximumSize)
                    {
                        File.Copy(FilePath, FilePath + ".old", true);
                        File.Delete(FilePath);
                    }
                    File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
                // Logging must never take the app down.
            }
        }
    }
}
