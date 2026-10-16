using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.PMD.Benchlab;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.PresentMonInterface
{
    public class PresentMonCaptureService : ICaptureService
    {
        // Temporary disabled ETW tracking
        // EtwBufferFillPct,EtwBuffersInUse,EtwTotalBuffers,EtwEventsLost,EtwBuffersLost

        // VidPnSourceId, LayerIndex and PresentId come from --write_display_metadata. PresentMon only
        // fills them for presents it sees in a multi-plane overlay flip event and writes 0 otherwise,
        // so PresentId 0 marks a row without display layer data.
        public static readonly string COLUMN_HEADER_WITH_PC_LATENCY =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,VidPnSourceId,LayerIndex,AllowsTearing,PresentMode," +
            "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency," +
            "MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy," +
            "MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency,PresentId";

        public static readonly string COLUMN_HEADER_WITHOUT_PC_LATENCY =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,VidPnSourceId,LayerIndex,AllowsTearing,PresentMode," +
            "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency," +
            "MsUntilDisplayed,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy," +
            "MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency,PresentId";

        private static readonly PresentMonColumnLayout ColumnLayoutWithPcLatency =
            new PresentMonColumnLayout(COLUMN_HEADER_WITH_PC_LATENCY, true);

        private static readonly PresentMonColumnLayout ColumnLayoutWithoutPcLatency =
            new PresentMonColumnLayout(COLUMN_HEADER_WITHOUT_PC_LATENCY, false);

        // Fixed indices before the optional PC latency column — identical in both layouts.
        // MsPCLatency itself has no fixed index; use the dynamic MsPcLatency_Index instead.
        public static readonly int ApplicationName_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "Application");
        public static readonly int ProcessID_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "ProcessID");
        public static readonly int SwapChainAddress_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "SwapChainAddress");
        // Graphics runtime/API of the presenting app (e.g. "DXGI", "D3D9") — index 3; used to
        // label the <APP> line of the hook-free OSD (RTSS gets this from the 3D API, we get it from
        // PresentMon). The in-game renderer replaces it with the API it proved (D3D11, D3D12, Vulkan).
        public static readonly int PresentRuntime_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "PresentRuntime");
        // "Application" or a generated-frame source (--track_frame_type); fixed index before the
        // optional PC latency column. Consumed by the hook-free feed diagnostics only.
        public static readonly int FrameType_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "FrameType");
        public static readonly int MsBetweenPresents_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "MsBetweenPresents");
        public static readonly int MsBetweenDisplayChange_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "MsBetweenDisplayChange");

        private readonly IAppConfiguration _appConfiguration;

        private PresentMonColumnLayout _activeColumnLayout;

        // Dynamic indices - derived from the active capture layout or current configuration.
        private PresentMonColumnLayout CurrentColumnLayout =>
            _activeColumnLayout ?? GetColumnLayout(_appConfiguration.UsePcLatency);

        public int CPUStartQPCTimeInMs_Index => Array.IndexOf(CurrentColumnLayout.Columns, "CPUStartQPCTimeInMs");
        public int StartTimeInMs_INDEX => Array.IndexOf(CurrentColumnLayout.Columns, "CPUStartQPCTimeInMs");
        public int CpuBusy_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsCPUBusy");
        public int GpuBusy_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsGPUBusy");
        public int AnimationError_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsAnimationError");
        // -1 when the running session was started without PC latency tracking
        public int MsPcLatency_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsPCLatency");

        // Custom PresentMon build - ETW tracking columns
        public int EtwBufferFillPct_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwBufferFillPct");
        public int EtwBuffersInUse_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwBuffersInUse");
        public int EtwTotalBuffers_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwTotalBuffers");
        public int EtwEventsLost_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwEventsLost");
        public int EtwBuffersLost_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwBuffersLost");
        public int ValidLineLength => CurrentColumnLayout.ValidLineLength;

        public string ColumnHeader => CurrentColumnLayout.ColumnHeader;

        // PresentMon exits within milliseconds when it cannot open its ETW session, so an instance
        // that is still alive after this window is up for good.
        private static readonly TimeSpan PRESENT_MON_SETTLE_TIME = TimeSpan.FromSeconds(2);

        // Upper bound for --terminate_existing_session to do its work, see StartCaptureService.
        private const int PRESENT_MON_TERMINATE_TIMEOUT_MS = 3000;

        private readonly ISubject<string[]> _outputDataStream;
        private readonly BehaviorSubject<bool> _captureServiceRunning = new BehaviorSubject<bool>(false);
        private readonly ILogger<PresentMonCaptureService> _logger;
        private readonly PresentMonProcessList _processList;
        private IDisposable _settleDisposable;
        private Process _presentMonProcess;
        private ChildProcessJob _presentMonJob;

        public Dictionary<string, int> ParameterNameIndexMapping { get; }

        public IObservable<string[]> FrameDataStream
            => _outputDataStream.AsObservable();
        public Subject<bool> IsCaptureModeActiveStream { get; }

        public bool IsCaptureServiceRunning => _captureServiceRunning.Value;

        public IObservable<bool> CaptureServiceRunningStream
            => _captureServiceRunning.AsObservable();

        public PresentMonCaptureService(ILogger<PresentMonCaptureService> logger, IAppConfiguration appConfiguration)
        {
            _outputDataStream = new Subject<string[]>();
            IsCaptureModeActiveStream = new Subject<bool>();
            _logger = logger;
            _processList = new PresentMonProcessList(logger);
            _appConfiguration = appConfiguration;
        }

        public bool StartCaptureService(IServiceStartInfo startinfo)
        {
            if (!CaptureServiceInfo.IsCompatibleWithRunningOS)
            {
                return false;
            }

            try
            {
                SetCaptureServiceRunning(false);
                TerminateRunningPresentMon();
                _processList.Start(_outputDataStream);
                var captureColumnLayout = GetColumnLayout(IsPcLatencyTrackingEnabled(startinfo));
                _activeColumnLayout = captureColumnLayout;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = startinfo.FileName,
                        Arguments = startinfo.Arguments,
                        UseShellExecute = startinfo.UseShellExecute,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true, // is it necessary?
                        CreateNoWindow = startinfo.CreateNoWindow,
                        Verb = "runas",
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        // The first line is the CSV header, written once the ETW session is up:
                        // data on stdout is the earliest proof that PresentMon runs properly.
                        SetCaptureServiceRunning(true);

                        var lineSplit = e.Data.Split(',');
                        if (HasValidLineLength(lineSplit, captureColumnLayout))
                        {
                            if (lineSplit[ApplicationName_INDEX] != "<error>")
                            {
                                _outputDataStream.OnNext(lineSplit);
                            }
                        }
                    }
                };

                process.Exited += (sender, e) =>
                {
                    // A later start already replaced this instance: its exit says nothing about
                    // the service that is running now.
                    if (ReferenceEquals(_presentMonProcess, process))
                        SetCaptureServiceRunning(false);
                };

                _presentMonProcess = process;
                process.Start();
                TieToThisProcess(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Nothing is written to stdout before the first present on an idle system, so
                // surviving the settle window is the fallback proof that the service is up.
                _settleDisposable?.Dispose();
                _settleDisposable = Observable.Timer(PRESENT_MON_SETTLE_TIME)
                    .Subscribe(_ => SetCaptureServiceRunning(IsAlive(process)));

                _logger.LogInformation("PresentMon successfully started");
                return true;
            }
            catch (Exception e)
            {
                _activeColumnLayout = null;
                SetCaptureServiceRunning(false);
                _logger.LogError(e, "Failed to start CaptureService");
                return false;
            }
        }

        public bool StopCaptureService()
        {
            _settleDisposable?.Dispose();
            _settleDisposable = null;
            _presentMonProcess = null;
            _activeColumnLayout = null;
            SetCaptureServiceRunning(false);

            try
            {
                _processList.Stop();

                TerminateRunningPresentMon();
                return true;
            }
            catch { return false; }

        }

        // PresentMon runs from the application folder. Without a job it outlives CapFrameX when the
        // app is killed or crashes, keeps that folder locked and goes on capturing in the background.
        // The job closes with CapFrameX and ends the instance with it; a regular stop still terminates
        // it first through --terminate_existing_session.
        private void TieToThisProcess(Process process)
        {
            try
            {
                var job = ChildProcessJob.Attach(process);
                // The previous instance was terminated before this one started.
                _presentMonJob?.Dispose();
                _presentMonJob = job;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PresentMon could not be tied to the CapFrameX process and may outlive it.");
            }
        }

        private void SetCaptureServiceRunning(bool isRunning)
        {
            if (_captureServiceRunning.Value != isRunning)
                _captureServiceRunning.OnNext(isRunning);
        }

        private static bool IsAlive(Process process)
        {
            try
            {
                return process != null && !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stops a running PresentMon instance and waits for the termination to finish.
        /// </summary>
        /// <remarks>
        /// --terminate_existing_session runs in a process of its own and only asks the running
        /// instance to stop, so a termination left in flight can take down the *next* instance
        /// instead of its predecessor - which is what a restart of the capture service does. With
        /// no instance running there is nothing to terminate and starting the terminator would
        /// only create that race: an ETW session orphaned by a crash is cleaned up by the
        /// --stop_existing_session every start carries anyway.
        /// </remarks>
        private static void TerminateRunningPresentMon()
        {
            if (!IsPresentMonRunning())
                return;

            var terminator = TryKillPresentMon();
            if (terminator == null)
                return;

            try
            {
                terminator.WaitForExit(PRESENT_MON_TERMINATE_TIMEOUT_MS);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while waiting for the PresentMon session to terminate.");
            }
            finally
            {
                terminator.Dispose();
            }
        }

        private static bool IsPresentMonRunning()
        {
            Process[] processes;

            try
            {
                processes = Process.GetProcessesByName(CaptureServiceConfiguration.PresentMonAppName);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while looking for a running PresentMon process.");
                return true;
            }

            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
            => _processList.GetAllFilteredProcesses(filter);

        /// <summary>
        /// Asks a running PresentMon instance to stop. Returns the terminating process so callers
        /// that start a new instance right after can wait for it, or null when it failed to start.
        /// </summary>
        public static Process TryKillPresentMon()
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine("PresentMon", $"{CaptureServiceConfiguration.PresentMonAppName}.exe"),
                        Arguments = "--terminate_existing_session",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas",
                    }
                };

                process.Start();
                return process;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while killing PresentMon process.");
                return null;
            }
        }

        private static PresentMonColumnLayout GetColumnLayout(bool usePcLatency)
        {
            return usePcLatency ? ColumnLayoutWithPcLatency : ColumnLayoutWithoutPcLatency;
        }

        private static bool IsPcLatencyTrackingEnabled(IServiceStartInfo startinfo)
        {
            return (startinfo?.Arguments?.IndexOf("--track_pc_latency", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        private static bool HasValidLineLength(string[] lineSplit, PresentMonColumnLayout columnLayout)
        {
            return lineSplit?.Length == columnLayout.ValidLineLength;
        }

        private sealed class PresentMonColumnLayout
        {
            public PresentMonColumnLayout(string columnHeader, bool usePcLatency)
            {
                ColumnHeader = columnHeader;
                Columns = columnHeader.Split(',');
                ValidLineLength = Columns.Length;
                UsePcLatency = usePcLatency;
            }

            public string ColumnHeader { get; }

            public string[] Columns { get; }

            public int ValidLineLength { get; }

            public bool UsePcLatency { get; }
        }
    }
}
