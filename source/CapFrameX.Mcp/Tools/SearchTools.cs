using CapFrameX.Mcp.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class SearchTools
    {
        private readonly RecordTools _recordTools;

        public SearchTools(RecordTools recordTools)
        {
            _recordTools = recordTools;
        }

        [McpServerTool(Name = "cfx_search_records",
            Description = "Free-text search across records. Matches against game name, process name, comment, " +
                "CPU, GPU, OS, motherboard, and RAM strings of the recorded system info. Case-insensitive.")]
        public List<RecordSummary> SearchRecords(
            [Description("Query string. Substring match across game/comment/hardware fields.")] string query,
            [Description("Optional limit on number of results. 0 or omitted = no limit.")] int limit = 0)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<RecordSummary>();

            var dir = _recordTools.ResolveRecordsDirectory();
            if (dir == null || !dir.Exists)
                return new List<RecordSummary>();

            var hits = new List<RecordSummary>();
            foreach (var fi in dir.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => f.LastWriteTimeUtc))
            {
                var session = _recordTools.SafeLoad(fi.FullName);
                if (session?.Info == null) continue;

                if (!Matches(session.Info, query)) continue;

                hits.Add(RecordTools.BuildSummary(fi.FullName, session));
                if (limit > 0 && hits.Count >= limit) break;
            }
            return hits;
        }

        private static bool Matches(CapFrameX.Data.Session.Contracts.ISessionInfo info, string query)
        {
            return Contains(info.GameName, query)
                || Contains(info.ProcessName, query)
                || Contains(info.Comment, query)
                || Contains(info.Processor, query)
                || Contains(info.GPU, query)
                || Contains(info.OS, query)
                || Contains(info.Motherboard, query)
                || Contains(info.SystemRam, query);
        }

        private static bool Contains(string source, string query) =>
            !string.IsNullOrEmpty(source)
            && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
