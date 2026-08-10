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
                        // CapFrameX's Serilog logger runs at MinimumLevel.Debug, so it would
                        // otherwise capture every PmcReader trace line. We therefore forward only
                        // selected levels to keep CapFrameX.log readable:
                        //   Error / Warning -> critical issues (driver management, hardware access)
                        //   Info            -> one-time startup milestones (plugin / driver init)
                        //   Debug           -> verbose per-operation detail; NOT forwarded, it stays
                        //                      in the dedicated %TEMP%\PmcReaderDiagnostics.log only.
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
                                // Debug stays out of the application log on purpose.
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
