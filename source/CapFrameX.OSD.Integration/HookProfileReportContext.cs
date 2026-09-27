using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using CapFrameX.OverlayReporting;

namespace CapFrameX.OSD.Integration
{
    internal sealed class HookProfileReportContext
    {
        internal ReportBinary Game;
        internal ReportBinary Hook;
        internal List<ReportBinary> Modules = new List<ReportBinary>();
        internal List<ReportGpu> Gpus = new List<ReportGpu>();
        internal string Status;
        private readonly Dictionary<string, (long Size, DateTime Modified, ReportBinary Binary)> _binaryCache
            = new Dictionary<string, (long, DateTime, ReportBinary)>(StringComparer.OrdinalIgnoreCase);

        internal HookProfileReportContext Capture(int pid, string gamePath,
            string hookPath, CancellationToken cancellationToken)
        {
            // Called only by the consent-gated background worker, never by a Present callback.
            var result = new HookProfileReportContext
            {
                Game = ReadCached(gamePath, cancellationToken),
                Hook = string.IsNullOrEmpty(hookPath) ? null : ReadCached(hookPath, cancellationToken)
            };
            cancellationToken.ThrowIfCancellationRequested();
            bool modulesAvailable = HookModuleScanner.TryEnumerate(pid, out var modules, out _);
            if (modulesAvailable)
            {
                foreach (HookModuleRecord module in modules.Where(m => IsRelevantModule(m.Name)).Take(48))
                    result.Modules.Add(ReadCached(module.Path, cancellationToken));
            }
            bool gpusAvailable = false;
            try
            {
                using var searcher = new ManagementObjectSearcher(new ManagementScope("root\\CIMV2"),
                    new ObjectQuery("SELECT Name, DriverVersion, PNPDeviceID FROM Win32_VideoController"),
                    new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(2) });
                using var devices = searcher.Get();
                foreach (ManagementObject device in devices)
                {
                    using (device)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string id = device["PNPDeviceID"] as string ?? "";
                        result.Gpus.Add(new ReportGpu
                        {
                            Name = Clean(device["Name"] as string),
                            DriverVersion = Clean(device["DriverVersion"] as string),
                            VendorId = PciPart(id, "VEN"), DeviceId = PciPart(id, "DEV")
                        });
                    }
                    if (result.Gpus.Count == 8) break;
                }
                gpusAvailable = true;
            }
            catch (Exception ex) when (ex is ManagementException || ex is UnauthorizedAccessException ||
                                       ex is System.Runtime.InteropServices.COMException)
            {
            }
            result.Status = modulesAvailable
                ? (gpusAvailable ? "complete" : "gpu-unavailable")
                : (gpusAvailable ? "modules-unavailable" : "modules-and-gpu-unavailable");
            return result;
        }

        private ReportBinary ReadCached(string path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var file = new FileInfo(path);
                if (_binaryCache.TryGetValue(path, out var cached) && cached.Size == file.Length &&
                    cached.Modified == file.LastWriteTimeUtc) return cached.Binary;
                var binary = ReadBinary(path, token);
                if (_binaryCache.Count >= 128) _binaryCache.Clear();
                _binaryCache[path] = (file.Length, file.LastWriteTimeUtc, binary);
                return binary;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                return ReadBinary(path, token);
            }
        }

        internal static bool IsRelevantModule(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n.StartsWith("sl.") || n.StartsWith("nvngx_") ||
                n.StartsWith("amd_fidelityfx") || n.StartsWith("ffx_") ||
                n.StartsWith("libxess") || n.StartsWith("dlssg_to_fsr3") ||
                n.StartsWith("optiscaler") || n.StartsWith("reshade") ||
                n.StartsWith("rtsshooks") || n.StartsWith("eosovh") ||
                n.StartsWith("gameoverlayrenderer") || n.StartsWith("discordhook") ||
                n.StartsWith("cfx_osd_") || n == "dxgi.dll" || n == "d3d12.dll" ||
                n == "d3d12core.dll" || n == "d3d11.dll" || n == "vulkan-1.dll";
        }

        internal static ReportBinary ReadBinary(string path, CancellationToken cancellationToken)
        {
            var binary = new ReportBinary { Name = Clean(Path.GetFileName(path ?? "")) };
            if (string.IsNullOrWhiteSpace(path)) return binary;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string systemRoot = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                binary.Location = Path.GetFullPath(path).StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase)
                    ? "system" : "application";
                var version = FileVersionInfo.GetVersionInfo(path);
                binary.FileVersion = Clean(version.FileVersion);
                binary.ProductVersion = Clean(version.ProductVersion);
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan);
                binary.Size = stream.Length;
                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    if (stream.Length >= 64 && reader.ReadUInt16() == 0x5a4d)
                    {
                        stream.Position = 0x3c;
                        int offset = reader.ReadInt32();
                        if (offset >= 64 && offset <= stream.Length - 6)
                        {
                            stream.Position = offset;
                            if (reader.ReadUInt32() == 0x00004550)
                            {
                                ushort machine = reader.ReadUInt16();
                                binary.Architecture = machine == 0x8664 ? "x64" :
                                    machine == 0x014c ? "x86" : machine == 0xaa64 ? "arm64" : "unknown";
                            }
                        }
                    }
                }
                // Versions and size remain available for unusually large or inaccessible binaries.
                if (stream.Length > 512L * 1024 * 1024)
                {
                    binary.ReadStatus = "hash-size-limit";
                    return binary;
                }
                stream.Position = 0;
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[65536];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    hash.AppendData(buffer, 0, count);
                }
                binary.Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                binary.ReadStatus = "complete";
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is ArgumentException || ex is System.ComponentModel.Win32Exception)
            {
                binary.ReadStatus = "unavailable";
            }
            return binary;
        }

        internal static string Clean(string text)
            => new string((text ?? "").Where(c => !char.IsControl(c) && c != '\\' && c != '/')
                .Take(160).ToArray());

        private static string PciPart(string id, string prefix)
            => Regex.Match(id, prefix + "_([0-9a-fA-F]{4})", RegexOptions.IgnoreCase).Groups[1].Value.ToUpperInvariant();
    }
}
