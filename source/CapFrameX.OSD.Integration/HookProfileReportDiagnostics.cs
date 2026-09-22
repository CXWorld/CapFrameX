using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CapFrameX.OverlayReporting;

namespace CapFrameX.OSD.Integration
{
    public sealed partial class HookProfileReportService
    {
        internal void ObserveHost(int pid, string runtime, bool overlayRequested,
            string fallbackSource, string fallbackReason, Func<int, string> readWindow = null)
        {
            if (!IsEnabled) return;
            // Even the small window query is opt-in. No window handles, titles or other PIDs
            // leave this method. Query at most once a second; record only a state transition.
            lock (_gate)
            {
                if (!_consented || _disposed || _disposing || !_sessions.TryGetValue(pid, out var session)) return;
                long now = _tickCount();
                if (session.WindowState == null || now - session.WindowReadMs >= 1000)
                {
                    session.WindowState = (readWindow ?? ReadWindowState)(pid);
                    session.WindowReadMs = now;
                }
                var host = new ReportHostState
                {
                    Runtime = runtime == "DXGI" || runtime == "D3D11" || runtime == "D3D12" ||
                        runtime == "D3D9" || runtime == "OpenGL" || runtime == "Vulkan"
                        ? runtime : "unknown",
                    Window = session.WindowState, OverlayRequested = overlayRequested,
                    FallbackSource = fallbackSource == "target-policy" || fallbackSource == "vulkan" ||
                        fallbackSource == "native" || fallbackSource == "runtime" ? fallbackSource : "none",
                    FallbackReason = FallbackCode(fallbackReason)
                };
                string key = $"{host.Runtime}:{host.Window}:{host.OverlayRequested}:{host.FallbackSource}:{host.FallbackReason}";
                Record(pid, new ReportEvent { Kind = "host-state", Host = host }, key);
            }
        }

        internal static string FallbackCode(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "none";
            // Log messages can contain filenames and exception details. Transmit only these
            // fixed reason codes, never the message itself.
            string value = reason.ToLowerInvariant();
            if (value.Contains("injection blocked")) return "target-blocked";
            if (value.Contains("unsupported")) return "unsupported-runtime";
            if (value.Contains("injection failed")) return "injection-failed";
            if (value.Contains("status") && value.Contains("unavailable") || value.Contains("publish status")) return "status-timeout";
            if (value.Contains("early injection")) return "early-injection-required";
            if (value.Contains("queue")) return "queue-unavailable";
            if (value.Contains("frame-generation") || value.Contains("frame generation")) return "foreign-presenter";
            if (value.Contains("finish installing")) return "install-hung";
            if (value.Contains("installation failed")) return "install-failed";
            if (value.Contains("creation failed")) return "renderer-create-failed";
            if (value.Contains("did not initialize")) return "renderer-stalled";
            if (value.Contains("did not observe") && value.Contains("present")) return "no-present";
            if (value.Contains("vulkan")) return "vulkan-composite-unavailable";
            return "unspecified";
        }

        private static string ReadWindowState(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                IntPtr window = process.MainWindowHandle;
                if (window == IntPtr.Zero) return "unknown";
                if (IsIconic(window)) return "minimized";
                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero) return "unknown";
                GetWindowThreadProcessId(foreground, out uint foregroundPid);
                return foregroundPid == (uint)pid ? "foreground" : "background";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException ||
                                       ex is System.ComponentModel.Win32Exception || ex is NotSupportedException)
            {
                return "unknown";
            }
        }

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    }
}
