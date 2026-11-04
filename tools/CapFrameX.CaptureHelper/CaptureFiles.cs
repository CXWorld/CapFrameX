using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CapFrameX.CaptureHelper
{
    internal static class CaptureWriter
    {
        /// <summary>Writes PresentMon's header and the captured rows as they came; returns the file path.</summary>
        public static string Write(string folder, CaptureResult capture)
        {
            Directory.CreateDirectory(folder);
            var path = CreatePath(folder, capture.ProcessName, capture.StartTime);
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(capture.Header);
                foreach (var row in capture.Rows)
                    writer.WriteLine(row);
            }
            return path;
        }

        private static string CreatePath(string folder, string processName, DateTime startTime)
        {
            var name = string.IsNullOrWhiteSpace(processName) ? "capture" : processName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name.Substring(0, name.Length - 4);
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            var baseName = $"{name}_{startTime:yyyy-MM-dd_HH-mm-ss}";
            var path = Path.Combine(folder, baseName + ".csv");
            for (int index = 2; File.Exists(path); index++)
                path = Path.Combine(folder, $"{baseName}_{index}.csv");
            return path;
        }
    }

    internal sealed class CaptureFile
    {
        private CaptureFile(string processName, double[] frametimes)
        {
            ProcessName = processName;
            Frametimes = frametimes;
        }

        public string ProcessName { get; }

        /// <summary>msBetweenPresents of every frame, in milliseconds.</summary>
        public double[] Frametimes { get; }

        /// <summary>
        /// Reads a PresentMon CSV file: the helper's own captures, files PresentMon wrote itself
        /// (UTF-16) and PresentMon 2.x files (FrameTime). A file with several processes yields the
        /// one with the most frames.
        /// </summary>
        public static CaptureFile Load(string path)
        {
            int applicationColumn = -1, processIdColumn = -1, frametimeColumn = -1;
            bool headerRead = false;
            var frametimesByProcess = new Dictionary<string, List<double>>(StringComparer.Ordinal);
            var processNames = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var line in File.ReadLines(path))
            {
                if (line.Length == 0) continue;
                var fields = line.Split(',');
                if (!headerRead)
                {
                    headerRead = true;
                    applicationColumn = IndexOf(fields, "Application");
                    processIdColumn = IndexOf(fields, "ProcessID");
                    frametimeColumn = IndexOf(fields, "msBetweenPresents");
                    if (frametimeColumn < 0) frametimeColumn = IndexOf(fields, "FrameTime");
                    if (frametimeColumn < 0)
                        throw new InvalidDataException("This is not a PresentMon CSV file: it has no msBetweenPresents column.");
                    continue;
                }

                if (frametimeColumn >= fields.Length
                    || !double.TryParse(fields[frametimeColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out double frametime)
                    || !(frametime > 0))
                    continue;

                var name = Field(fields, applicationColumn);
                var key = name + "|" + Field(fields, processIdColumn);
                if (!frametimesByProcess.TryGetValue(key, out var frametimes))
                {
                    frametimesByProcess.Add(key, frametimes = new List<double>());
                    processNames.Add(key, name);
                }
                frametimes.Add(frametime);
            }

            if (!headerRead) throw new InvalidDataException("The file is empty.");
            if (frametimesByProcess.Count == 0) return new CaptureFile("", Array.Empty<double>());
            var main = frametimesByProcess.OrderByDescending(process => process.Value.Count).First();
            return new CaptureFile(processNames[main.Key], main.Value.ToArray());
        }

        private static string Field(string[] fields, int column) =>
            column >= 0 && column < fields.Length ? fields[column] : "";

        private static int IndexOf(string[] fields, string name) =>
            Array.FindIndex(fields, field => string.Equals(field.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }
}
