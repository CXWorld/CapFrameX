#if DEBUG
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CapFrameX
{
    /// <summary>
    /// Writes WPF data binding failures into the application log while debugging. Visual Studio
    /// shows the same messages, but not where they come from: a binding that cannot find its
    /// ancestor reports a target element without a place in the tree. So the first failures of
    /// that kind also log a snapshot of the data grids of every window with the view that hosts
    /// them, since cells and headers are the usual targets (the MDIX styles look up the row and
    /// the grid; a virtualized or re-templated grid hands out cells and headers without them).
    ///
    /// Expected once per process: the first use of MaterialDesign's action toggle button and
    /// circular progress bar templates reports "Cannot find governing FrameworkElement" for the
    /// bindings inside their clip geometries and arc segments (8 and 13 lines). WPF instantiates
    /// those Freezables once when it parses the template, whatever the first use is.
    /// </summary>
    internal static class BindingFailureLog
    {
        // Trace ids of System.Windows.Data: 2 "cannot find governing FrameworkElement",
        // 4 "cannot find source for binding with reference", 40 "path error".
        private const int CannotFindSourceId = 4;
        private const int MaxSnapshots = 6;
        private static int _snapshots;

        public static void Attach()
        {
            // Refresh creates the trace sources and resets their switches, so it comes first.
            PresentationTraceSources.Refresh();
            var source = PresentationTraceSources.DataBindingSource;
            if (source.Listeners.OfType<Listener>().Any()) return;
            source.Listeners.Add(new Listener());
            source.Switch.Level = SourceLevels.Warning;
        }

        private sealed class Listener : TraceListener
        {
            public override void Write(string message) { }

            public override void WriteLine(string message) => Log.Logger.Warning("WPF binding: {message}", message);

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
                => Record(eventType, id, message);

            public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
                => Record(eventType, id, args == null || args.Length == 0 ? format : string.Format(format, args));

            private static void Record(TraceEventType eventType, int id, string message)
            {
                Log.Logger.Warning("WPF binding {eventType} {id}: {message}", eventType, id, message);
                if (id != CannotFindSourceId || _snapshots >= MaxSnapshots) return;
                // The frames above the binding engine say whether this is WPF's deferred last
                // attempt (a dispatcher operation) or a lookup forced while something else ran.
                var frames = new StackTrace(skipFrames: 3, fNeedFileInfo: false).GetFrames()
                    .Select(frame => frame.GetMethod())
                    .Where(method => method?.DeclaringType != null)
                    .Select(method => method.DeclaringType.FullName + "." + method.Name)
                    .Where(name => !name.StartsWith("System.Windows.Data.BindingExpression", StringComparison.Ordinal))
                    .Take(30);
                Log.Logger.Warning("WPF binding call path:{newLine}    at {frames}", Environment.NewLine,
                    string.Join(Environment.NewLine + "    at ", frames));
                _snapshots++;
                try
                {
                    Log.Logger.Warning("WPF binding snapshot {number}:{newLine}{snapshot}", _snapshots, Environment.NewLine, Snapshot());
                }
                catch (Exception ex)
                {
                    Log.Logger.Warning(ex, "WPF binding snapshot failed");
                }
            }
        }

        private static string Snapshot()
        {
            var text = new StringBuilder();
            foreach (Window window in Application.Current.Windows)
            {
                text.AppendLine($"  window {window.GetType().Name} loaded={window.IsLoaded} visible={window.IsVisible}");
                foreach (var grid in Descendants<DataGrid>(window))
                {
                    int rows = Descendants<DataGridRow>(grid).Count();
                    int cells = Descendants<DataGridCell>(grid).Count();
                    int headers = Descendants<DataGridColumnHeader>(grid).Count();
                    text.AppendLine($"    DataGrid '{grid.Name}' in {HostView(grid)}: columns={grid.Columns.Count} items={grid.Items.Count} " +
                        $"rows={rows} cells={cells} headers={headers} loaded={grid.IsLoaded} visible={grid.IsVisible} size={grid.ActualWidth:F0}x{grid.ActualHeight:F0}");
                }
            }
            return text.ToString().TrimEnd();
        }

        private static string HostView(DependencyObject element)
        {
            var names = new List<string>();
            for (var current = element; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is UserControl view && current != element) names.Add(view.GetType().Name);
                if (current is TabItem tab) names.Add($"tab '{tab.Header}'");
            }
            return names.Count == 0 ? "no view" : string.Join(" < ", names);
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var nested in Descendants<T>(child)) yield return nested;
            }
        }
    }
}
#endif
