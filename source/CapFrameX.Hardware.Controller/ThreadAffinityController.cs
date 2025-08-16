using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Hardware.CPU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Hardware.Controller
{
    public enum HybridCore
    {
        Performance,
        Efficiency,
        Dense
    }

    public enum AffinityState
    {
        Default,
        PCores,
        ECores,
        CCD0,
        CCD1,
        DCores
    }

    public class ThreadAffinityController : IThreadAffinityController
    {
        private const uint CPUID_CORE_MASK_STATUS = 0x1A;

        private CPUID[] _threads;
        private CPUID[][] _coreThreads;
        private Vendor _vendor;
        private int _currentProcessId;

        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<ThreadAffinityController> _logger;
        private readonly Dictionary<int, HybridCore> _hybridCoreDict = new Dictionary<int, HybridCore>();
        private readonly Dictionary<AffinityState, AffinityState> _intelAffinityStateTransitions =
            new Dictionary<AffinityState, AffinityState>()
            {
                { AffinityState.Default, AffinityState.PCores },
                { AffinityState.PCores, AffinityState.ECores },
                { AffinityState.ECores, AffinityState.Default }
            };
        private readonly Dictionary<AffinityState, AffinityState> _amdAffinityStateTransitions =
            new Dictionary<AffinityState, AffinityState>()
            {
                { AffinityState.Default, AffinityState.CCD0},
                { AffinityState.CCD0, AffinityState.CCD1},
                { AffinityState.CCD1, AffinityState.Default}
            };

        private readonly Dictionary<AffinityState, AffinityState> _amdHybridAffinityStateTransitions =
            new Dictionary<AffinityState, AffinityState>()
            {
                { AffinityState.Default, AffinityState.PCores},
                { AffinityState.PCores, AffinityState.DCores},
                { AffinityState.DCores, AffinityState.Default}
            };

        private bool _isSupportedCPU = false;
        private AffinityState _currentAffinityState = AffinityState.Default;
        private IntPtr _defaultProcessAffinity;

        public AffinityState CpuAffinityState => _currentAffinityState;

        public ThreadAffinityController(IAppConfiguration appConfiguration,
            IRTSSService rTSSService,
            ILogger<ThreadAffinityController> logger,
            ISensorService sensorService)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;

            rTSSService.ProcessIdStream.Subscribe(id =>
            {
                // reset when process changed
                if (id == 0 || _currentProcessId != id)
                {
                    _currentAffinityState = AffinityState.Default;
                }

                // update process ID
                _currentProcessId = id;
            });

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await sensorService.SensorServiceCompletionSource.Task;
                    await Task.Delay(500);

                    CPUID[][] processorThreads = CPUGroup.GetProcessorThreads();
                    _threads = processorThreads.First();
                    _vendor = _threads[0].Vendor;

                    if (_threads.Length > 0)
                    {
                        _coreThreads = CPUGroup.GroupThreadsByCore(_threads);

                        switch (_threads[0].Vendor)
                        {
                            case Vendor.Intel:
                                // Intel (Hybrid)
                                {
                                    if (CpuArchitecture.IsHybridDesign(_threads))
                                    {
                                        for (int i = 0; i < _threads.Length; i++)
                                        {
                                            var previousAffinity = ThreadAffinity.Set(_threads[i].Affinity);
                                            if (Opcode.Cpuid(CPUID_CORE_MASK_STATUS, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                                            {
                                                switch (eax >> 24)
                                                {
                                                    case 0x20: _hybridCoreDict.Add(i, HybridCore.Efficiency); break;
                                                    case 0x40: _hybridCoreDict.Add(i, HybridCore.Performance); break;
                                                    default: break;
                                                }
                                            }

                                            ThreadAffinity.Set(previousAffinity);
                                        }

                                        _isSupportedCPU = true;
                                    }
                                }
                                break;
                            case Vendor.AMD:
                                {
                                    // AMD (Hybrid)
                                    if (CpuArchitecture.IsHybridDesign(_threads))
                                    {
                                        for (int i = 0; i < _threads.Length; i++)
                                        {
                                            var previousAffinity = ThreadAffinity.Set(_threads[i].Affinity);
                                            if (Opcode.Cpuid(0x80000026, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                                            {
                                                // Heterogeneous core topology supported
                                                if ((eax & (1u << 30)) != 0)
                                                {
                                                    uint coreType = (ebx >> 28) & 0xF;
                                                    switch (coreType)
                                                    {
                                                        case 0: _hybridCoreDict.Add(i, HybridCore.Performance); break;
                                                        case 1: _hybridCoreDict.Add(i, HybridCore.Dense); break;
                                                        default: break;
                                                    }
                                                }
                                            }

                                            ThreadAffinity.Set(previousAffinity);
                                        }

                                        _isSupportedCPU = true;
                                    }
                                    else
                                    {
                                        switch (_threads[0].Family)
                                        {
                                            case 0x17:
                                            case 0x19:
                                            case 0x60:
                                            case 0x61:
                                            case 0x70:
                                            case 0x1A:
                                            case 0x75:
                                            case 0x44:
                                                // Ryzen (2 CCDs)
                                                {
                                                    if (_coreThreads[0][0].Name.Contains("900") || _coreThreads[0][0].Name.Contains("950"))
                                                    {
                                                        if (_coreThreads.Length > 8)
                                                        {
                                                            _isSupportedCPU = true;
                                                        }
                                                    }
                                                }
                                                break;
                                        }
                                        break;
                                    }
                                } 
                                break;
                            default:
                                break;
                        }
                    }

                    _logger.LogDebug("{componentName} Ready", this.GetType().Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error wile initializing Thread Affinity Controller");
                }
            });
        }

        public void ToggleAffinity()
        {
            if (!_appConfiguration.UseThreadAffinity || !_isSupportedCPU)
                return;

            try
            {
                var process = Process.GetProcessById(_currentProcessId);

                if (process != null)
                {
                    if (_currentAffinityState == AffinityState.Default)
                    {
                        _defaultProcessAffinity = process.ProcessorAffinity;
                    }

                    if (_vendor == Vendor.AMD)
                    {
                        if (_hybridCoreDict.Any())
                        {
                            _currentAffinityState = _amdHybridAffinityStateTransitions[_currentAffinityState];
                        }
                        else
                        {
                            _currentAffinityState = _amdAffinityStateTransitions[_currentAffinityState];
                        }
                    }
                    else if (_vendor == Vendor.Intel)
                    {
                        _currentAffinityState = _intelAffinityStateTransitions[_currentAffinityState];
                    }

                    SetThreadAffinity(process);
                }
                else
                {
                    _currentAffinityState = AffinityState.Default;
                }
            }
            catch (Exception ex)
            {
                _currentAffinityState = AffinityState.Default;
                _logger.LogError(ex, "Error wile setting thread affinity");
            }
        }

        private void SetThreadAffinity(Process process)
        {
            if (_currentAffinityState == AffinityState.Default)
            {
                process.ProcessorAffinity = _defaultProcessAffinity;
                return;
            }

            long affinity = 0;

            if (_vendor == Vendor.AMD)
            {
                if (_hybridCoreDict.Any())
                {
                    int denseCoreCount = _hybridCoreDict.Count(item => item.Value == HybridCore.Dense);

                    if (_currentAffinityState == AffinityState.PCores)
                    {
                        var pCores = _hybridCoreDict.Where(core => core.Value == HybridCore.Performance);
                        affinity = GetBitMaskCpuIndex(pCores.First().Key);

                        foreach (var core in pCores.Skip(1))
                        {
                            affinity |= GetBitMaskCpuIndex(core.Key);
                        }
                    }
                    else if (_currentAffinityState == AffinityState.DCores && denseCoreCount > 0)
                    {
                        var dCores = _hybridCoreDict.Where(core => core.Value == HybridCore.Dense);
                        affinity = GetBitMaskCpuIndex(dCores.First().Key);

                        foreach (var core in dCores.Skip(1))
                        {
                            affinity |= GetBitMaskCpuIndex(core.Key);
                        }
                    }
                }
                else  // AMD homogenous architecture (2 CCDs)
                {
                    if (_currentAffinityState == AffinityState.CCD0)
                    {
                        affinity = GetBitMaskCpuIndex(0);

                        for (int i = 1; i < _threads.Length / 2; i++)
                        {
                            affinity |= GetBitMaskCpuIndex(i);
                        }
                    }
                    else if (_currentAffinityState == AffinityState.CCD1)
                    {
                        affinity = GetBitMaskCpuIndex(_threads.Length / 2);

                        for (int i = _threads.Length / 2 + 1; i < _threads.Length; i++)
                        {
                            affinity |= GetBitMaskCpuIndex(i);
                        }
                    }
                }
            }
            else if (_vendor == Vendor.Intel)
            {
                int efficiencyCoreCount = _hybridCoreDict.Count(item => item.Value == HybridCore.Efficiency);

                if (_currentAffinityState == AffinityState.PCores)
                {
                    var pCores = _hybridCoreDict.Where(core => core.Value == HybridCore.Performance);
                    affinity = GetBitMaskCpuIndex(pCores.First().Key);

                    foreach (var core in pCores.Skip(1))
                    {
                        affinity |= GetBitMaskCpuIndex(core.Key);
                    }
                }
                else if (_currentAffinityState == AffinityState.ECores && efficiencyCoreCount > 0)
                {
                    var eCores = _hybridCoreDict.Where(core => core.Value == HybridCore.Efficiency);
                    affinity = GetBitMaskCpuIndex(eCores.First().Key);

                    foreach (var core in eCores.Skip(1))
                    {
                        affinity |= GetBitMaskCpuIndex(core.Key);
                    }
                }
            }

            process.ProcessorAffinity = (IntPtr)affinity;
        }

        private static long GetBitMaskCpuIndex(int i) => 0x00000001L << i;
    }
}
