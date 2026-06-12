using System;
using PmcReader;
using Serilog;

namespace CapFrameX.PmcReader.Plugin
{
    /// <summary>
    /// Binds the PmcReader core's dependency-free diagnostics to the CapFrameX
    /// application logger (Serilog). The PmcReader core stays Serilog-free so it can
    /// also be built standalone; the host plugin layer installs this bridge once.
    /// </summary>
    internal static class PmcReaderLogging
    {
        private static readonly object Sync = new object();
        private static bool _initialized;

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_initialized)
                    return;

                _initialized = true;

                PmcDiagnostics.ExternalSink = (level, message, exception) =>
                {
                    try
                    {
                        ILogger logger = Log.Logger;
                        switch (level)
                        {
                            case PmcDiagnostics.Level.Error:
                                logger.Error(exception, "[PmcReader] {Message}", message);
                                break;
                            case PmcDiagnostics.Level.Warning:
                                logger.Warning("[PmcReader] {Message}", message);
                                break;
                            case PmcDiagnostics.Level.Info:
                                logger.Information("[PmcReader] {Message}", message);
                                break;
                            default:
                                logger.Debug("[PmcReader] {Message}", message);
                                break;
                        }
                    }
                    catch
                    {
                        // Logging must never break sensor processing.
                    }
                };
            }
        }
    }
}
