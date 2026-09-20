using System;
using System.IO;

namespace PmcReader
{
    /// <summary>
    /// Diagnostic logging for PMC reader debugging.
    /// Always writes to %TEMP%\PmcReaderDiagnostics.log and, when a host application
    /// wires up <see cref="ExternalSink"/>, forwards every entry there as well
    /// (CapFrameX binds this to its Serilog logger). Kept free of any third-party
    /// logging dependency so the shared PmcReader core also builds standalone.
    /// </summary>
    public static class PmcDiagnostics
    {
        public enum Level
        {
            Debug,
            Info,
            Warning,
            Error
        }

        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "PmcReaderDiagnostics.log");

        private static readonly object Lock = new object();
        private static bool _initialized;

        /// <summary>
        /// Optional external log sink. The CapFrameX plugin wires this to the
        /// application's Serilog logger so PMC driver diagnostics land in the
        /// regular CapFrameX.log. Left null when PmcReader runs standalone.
        /// </summary>
        public static Action<Level, string, Exception> ExternalSink;

        public static void Log(string message) => Write(Level.Debug, message, null);

        public static void Log(string format, params object[] args)
            => Write(Level.Debug, SafeFormat(format, args), null);

        public static void Info(string message) => Write(Level.Info, message, null);

        public static void Info(string format, params object[] args)
            => Write(Level.Info, SafeFormat(format, args), null);

        public static void Warning(string message) => Write(Level.Warning, message, null);

        public static void Warning(string format, params object[] args)
            => Write(Level.Warning, SafeFormat(format, args), null);

        public static void Error(string message, Exception exception = null)
            => Write(Level.Error, message, exception);

        private static void Write(Level level, string message, Exception exception)
        {
            WriteToFile(level, message, exception);

            try
            {
                ExternalSink?.Invoke(level, message, exception);
            }
            catch
            {
                // Never let logging break the caller.
            }
        }

        private static void WriteToFile(Level level, string message, Exception exception)
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

                    string line = exception == null
                        ? $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}\n"
                        : $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message} | {exception}\n";

                    File.AppendAllText(LogPath, line);
                }
            }
            catch
            {
            }
        }

        private static string SafeFormat(string format, object[] args)
        {
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }
    }
}
