using System;
using System.IO;

namespace PmcReader
{
    /// <summary>
    /// Diagnostic logging for PMC reader debugging.
    /// Writes to %TEMP%\PmcReaderDiagnostics.log
    /// </summary>
    public static class PmcDiagnostics
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "PmcReaderDiagnostics.log");

        private static readonly object Lock = new object();
        private static bool _initialized;

        public static void Log(string message)
        {
            try
            {
                lock (Lock)
                {
                    if (!_initialized)
                    {
                        File.WriteAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] PmcReader Diagnostics Start\n");
                        _initialized = true;
                    }
                    File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch
            {
            }
        }

        public static void Log(string format, params object[] args)
        {
            Log(string.Format(format, args));
        }
    }
}
