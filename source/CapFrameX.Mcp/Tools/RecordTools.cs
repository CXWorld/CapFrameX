using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Mcp.Attributes;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class RecordTools
    {
        private readonly IAppConfiguration _config;
        private readonly IRecordManager _records;

        public RecordTools(IAppConfiguration config, IRecordManager records)
        {
            _config = config;
            _records = records;
        }

        [McpServerTool(Name = "cfx_list_records",
            Description = "Lists capture records from the configured records directory. Each record is identified by its absolute file path (the 'id'), which can be passed to cfx_get_record, cfx_get_metrics, etc.")]
        public List<RecordSummary> ListRecords(
            [Description("Optional case-insensitive substring filter on game name or process name")] string filter = null,
            [Description("Optional limit on number of records returned. 0 or omitted = no limit.")] int limit = 0)
        {
            var dir = ResolveRecordsDirectory();
            if (dir == null || !dir.Exists)
            {
                Log.Logger.Warning("MCP cfx_list_records: records directory not found ({path})", dir?.FullName);
                return new List<RecordSummary>();
            }

            var files = dir.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            var summaries = new List<RecordSummary>(files.Count);
            foreach (var fi in files)
            {
                var session = SafeLoad(fi.FullName);
                if (session?.Info == null) continue;

                var summary = BuildSummary(fi.FullName, session);

                if (!string.IsNullOrEmpty(filter))
                {
                    var f = filter;
                    bool matches =
                        (summary.Game?.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (summary.ProcessName?.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!matches) continue;
                }

                summaries.Add(summary);
                if (limit > 0 && summaries.Count >= limit) break;
            }
            return summaries;
        }

        [McpServerTool(Name = "cfx_get_record",
            Description = "Returns full metadata for a single record (system info, run count, settings) given its id from cfx_list_records.")]
        public RecordDetail GetRecord(
            [Description("Record id (absolute file path) as returned by cfx_list_records")] string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("id must be provided", nameof(id));
            if (!File.Exists(id))
                throw new FileNotFoundException("Record not found", id);

            var session = SafeLoad(id);
            if (session?.Info == null)
                throw new InvalidOperationException("Record could not be loaded or is corrupt: " + id);

            var summary = BuildSummary(id, session);
            return new RecordDetail
            {
                Id = summary.Id,
                Game = summary.Game,
                ProcessName = summary.ProcessName,
                RecordedAt = summary.RecordedAt,
                DurationSec = summary.DurationSec,
                Comment = summary.Comment,
                Hash = summary.Hash,
                Runs = session.Runs?.Count ?? 0,
                ApiInfo = session.Info.ApiInfo,
                Resolution = session.Info.ResolutionInfo,
                PresentationMode = session.Info.PresentationMode,
                ResizableBar = session.Info.ResizableBar,
                Hags = session.Info.HAGS,
                WinGameMode = session.Info.WinGameMode,
                System = new RecordSystemInfo
                {
                    Cpu = session.Info.Processor,
                    Gpu = session.Info.GPU,
                    Ram = session.Info.SystemRam,
                    Motherboard = session.Info.Motherboard,
                    Os = session.Info.OS,
                    GpuDriver = session.Info.GPUDriverVersion,
                },
            };
        }

        internal ISession SafeLoad(string path)
        {
            try { return _records.LoadData(path); }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "MCP: failed to load record {path}", path);
                return null;
            }
        }

        internal static RecordSummary BuildSummary(string path, ISession session)
        {
            var firstRun = session.Runs?.FirstOrDefault();
            double duration = 0.0;
            if (firstRun?.CaptureData?.TimeInSeconds != null && firstRun.CaptureData.TimeInSeconds.Length > 0)
            {
                duration = firstRun.CaptureData.TimeInSeconds[firstRun.CaptureData.TimeInSeconds.Length - 1];
            }

            return new RecordSummary
            {
                Id = path,
                Game = session.Info.GameName,
                ProcessName = session.Info.ProcessName,
                RecordedAt = session.Info.CreationDate,
                DurationSec = Math.Round(duration, 2),
                Comment = session.Info.Comment,
                Hash = session.Hash,
            };
        }

        internal DirectoryInfo ResolveRecordsDirectory()
        {
            var path = _config.ObservedDirectory;
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (path.StartsWith("MyDocuments\\", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("MyDocuments/", StringComparison.OrdinalIgnoreCase))
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                path = Path.Combine(docs, path.Substring("MyDocuments/".Length));
            }

            try { return new DirectoryInfo(path); }
            catch { return null; }
        }
    }
}
