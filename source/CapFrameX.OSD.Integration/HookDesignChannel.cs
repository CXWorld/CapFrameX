using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CapFrameX.OSD.Integration
{
    internal interface IHookDesignChannel : IDisposable
    {
        void Publish(int targetPid, string templateJson, string metricsJson, ulong designRevision, ulong metricsRevision);
        void Disable();
    }

    /// <summary>Versioned, bounded UTF-8 scene/telemetry transport shared with DXGI and Vulkan readers.</summary>
    internal sealed class HookDesignChannel : IHookDesignChannel
    {
        internal const string MapName = @"Global\CfxOsdDesignV1";
        internal const int HeaderSize = 64;
        internal const int PayloadCapacity = 1024 * 1024;
        internal const int MapSize = HeaderSize + 2 * PayloadCapacity;
        internal const uint Magic = 0x31445843;
        internal const int Version = 1;
        private readonly object _gate = new object();
        private IntPtr _mapping, _view;
        private int _sequence;
        private ulong _designRevision, _metricsRevision;
        private byte[] _template, _metrics;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal static HookDesignChannel Create(string name = MapName)
        {
            var channel = new HookDesignChannel();
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW("D:(A;;GA;;;WD)S:(ML;;NW;;;LW)", 1, out IntPtr descriptor, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the design channel security descriptor.");
            try
            {
                var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
                channel._mapping = CreateFileMappingW(new IntPtr(-1), ref security, 4, 0, MapSize, name);
                if (channel._mapping == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the overlay design channel.");
                channel._view = MapViewOfFile(channel._mapping, 2, 0, 0, (UIntPtr)MapSize);
                if (channel._view == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not map the overlay design channel.");
                channel._sequence = Marshal.ReadInt32(channel._view, 8) & ~1;
                channel.Disable();
                return channel;
            }
            catch
            {
                channel.Dispose();
                throw;
            }
            finally { LocalFree(descriptor); }
        }

        public void Publish(int targetPid, string templateJson, string metricsJson, ulong designRevision, ulong metricsRevision)
        {
            if (targetPid <= 0) { Disable(); return; }
            if (designRevision == 0 || metricsRevision == 0) throw new ArgumentOutOfRangeException(nameof(designRevision));
            lock (_gate)
            {
                if (_view == IntPtr.Zero) return;
                bool newDesign = _template == null || _designRevision != designRevision;
                bool newMetrics = _metrics == null || _metricsRevision != metricsRevision;
                var template = newDesign ? Encode(templateJson) : _template;
                var metrics = newMetrics ? Encode(metricsJson) : _metrics;
                BeginWrite();
                if (newDesign) Marshal.Copy(template, 0, IntPtr.Add(_view, HeaderSize), template.Length);
                if (newMetrics) Marshal.Copy(metrics, 0, IntPtr.Add(_view, HeaderSize + PayloadCapacity), metrics.Length);
                Marshal.WriteInt32(_view, 12, targetPid);
                Marshal.WriteInt32(_view, 16, 1);
                Marshal.WriteInt32(_view, 20, template.Length);
                Marshal.WriteInt32(_view, 24, metrics.Length);
                QueryPerformanceCounter(out long qpc);
                Marshal.WriteInt64(_view, 32, qpc);
                Marshal.WriteInt64(_view, 40, unchecked((long)designRevision));
                Marshal.WriteInt64(_view, 48, unchecked((long)metricsRevision));
                EndWrite();
                _template = template; _metrics = metrics;
                _designRevision = designRevision; _metricsRevision = metricsRevision;
            }
        }

        public void Disable()
        {
            lock (_gate)
            {
                if (_view == IntPtr.Zero) return;
                BeginWrite();
                Marshal.WriteInt32(_view, 12, 0);
                Marshal.WriteInt32(_view, 16, 0);
                Marshal.WriteInt32(_view, 20, 0);
                Marshal.WriteInt32(_view, 24, 0);
                Marshal.WriteInt64(_view, 32, 0);
                Marshal.WriteInt64(_view, 40, 0);
                Marshal.WriteInt64(_view, 48, 0);
                EndWrite();
            }
        }

        private void BeginWrite()
        {
            Marshal.WriteInt32(_view, 8, unchecked(++_sequence));
            Thread.MemoryBarrier();
            Marshal.WriteInt32(_view, 0, unchecked((int)Magic));
            Marshal.WriteInt32(_view, 4, Version);
            Marshal.WriteInt32(_view, 28, 0);
            Marshal.WriteInt64(_view, 56, 0);
        }

        private void EndWrite()
        {
            Thread.MemoryBarrier();
            Marshal.WriteInt32(_view, 8, unchecked(++_sequence));
        }

        internal static byte[] Encode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf('\0') >= 0)
                throw new ArgumentException("Design channel JSON must be nonempty and contain no embedded NUL.", nameof(json));
            byte[] bytes = Utf8.GetBytes(json);
            if (bytes.Length > PayloadCapacity) throw new ArgumentException("Design channel JSON exceeds the 1 MiB UTF-8 limit.", nameof(json));
            return bytes;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                Disable();
                if (_view != IntPtr.Zero) { UnmapViewOfFile(_view); _view = IntPtr.Zero; }
                if (_mapping != IntPtr.Zero) { CloseHandle(_mapping); _mapping = IntPtr.Zero; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileMappingW(IntPtr file, ref SecurityAttributes security, uint protect, uint high, uint low, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr mapping, uint access, uint high, uint low, UIntPtr length);
        [DllImport("kernel32.dll")]
        private static extern bool UnmapViewOfFile(IntPtr view);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long count);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, out int size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);
    }
}
