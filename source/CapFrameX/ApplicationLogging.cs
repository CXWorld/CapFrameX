using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace CapFrameX
{
    public static class ApplicationLogging
    {
        public const long FileSizeLimitBytes = 2 * 1024 * 1024;
        public const int RetainedFileCountLimit = 5;

#if DEBUG
        public const LogEventLevel MinimumLevel = LogEventLevel.Debug;
#else
        public const LogEventLevel MinimumLevel = LogEventLevel.Information;
#endif

        public static Logger CreateLogger(string logDirectory)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Is(MinimumLevel)
                .Enrich.FromLogContext()
                .WriteTo.Sink(new InMemorySink())
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "CapFrameX.log"),
                    fileSizeLimitBytes: FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: RetainedFileCountLimit,
                    formatter: new CompactJsonFormatter())
                .CreateLogger();
        }
    }
}
