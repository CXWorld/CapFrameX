using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Hardware.CPU;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CapFrameX.Hardware.Controller
{
	public enum HybridCore
	{
		Performance,
		Efficiency
	}

	public enum AffinityState
	{
		Default,
		PCores,
		CCD0,
		CCD1
	}

	public class ThreadAffinityController : IThreadAffinityController
	{
		private const uint CPUID_CORE_MASK_STATUS = 0x1A;

		private readonly CPUID[] _threads;
		private readonly CPUID[][] _coreThreads;
		private readonly Vendor _vendor;
		private readonly uint _family;
		private readonly uint _model;
		private readonly IAppConfiguration _appConfiguration;
		private readonly Dictionary<int, HybridCore> _hybridCoreDict = new Dictionary<int, HybridCore>();
		private readonly Dictionary<AffinityState, AffinityState> _intelAffinityStateTransitions = new Dictionary<AffinityState, AffinityState>()
		{
			{ AffinityState.Default, AffinityState.PCores },
			{ AffinityState.PCores, AffinityState.Default }
		};
		private readonly Dictionary<AffinityState, AffinityState> _amdAffinityStateTransitions = new Dictionary<AffinityState, AffinityState>()
		{
			{ AffinityState.Default, AffinityState.CCD0},
			{ AffinityState.CCD0, AffinityState.CCD1},
			{ AffinityState.CCD1, AffinityState.Default}
		};

		private bool _isSupportedCPU = false;
		private AffinityState _currentAffinityState = AffinityState.Default;
		private IntPtr _defaultProcessAffinity;

		public AffinityState CpuAffinityState => _currentAffinityState;

		public ThreadAffinityController(IAppConfiguration appConfiguration,
			ILogger<ThreadAffinityController> logger)
		{
			_appConfiguration = appConfiguration;

			CPUID[][] processorThreads = CPUGroup.GetProcessorThreads();

			_threads = processorThreads.First();
			_vendor = _threads[0].Vendor;

			if (_threads.Length > 0)
			{
				_coreThreads = CPUGroup.GroupThreadsByCore(_threads);
				_model = _coreThreads[0][0].Model;
				_family = _coreThreads[0][0].Family;

				switch (_threads[0].Vendor)
				{
					case Vendor.Intel:
						// Intel (Hybrid)
						{
							if (IsHybridDesign())
							{
								for (int i = 0; i < _coreThreads.Length; i++)
								{
									var previousAffinity = ThreadAffinity.Set(_coreThreads[i][0].Affinity);
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
						switch (_threads[0].Family)
						{
							case 0x17:
							case 0x19:
							case 0x60:
							case 0x61:
							case 0x70:
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
					default:
						break;
				}
			}
		}

		public void ToggleAffinity(int processId)
		{
			if (!_appConfiguration.UseThreadAffinity || !_isSupportedCPU ||
				(!_hybridCoreDict.Any(item => item.Value == HybridCore.Efficiency) && _vendor == Vendor.Intel))
				return;

			var process = Process.GetProcessById(processId);

			if (process != null)
			{
				if (_currentAffinityState == AffinityState.Default)
				{
					_defaultProcessAffinity = process.ProcessorAffinity;
				}

				if (_vendor == Vendor.AMD)
				{
					_currentAffinityState = _amdAffinityStateTransitions[_currentAffinityState];
				}
				else if (_vendor == Vendor.Intel)
				{
					_currentAffinityState = _intelAffinityStateTransitions[_currentAffinityState];
				}

				SetThreadAffinity(process);
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
			else if (_vendor == Vendor.Intel)
			{
				if (_currentAffinityState == AffinityState.PCores)
				{
					affinity = GetBitMaskCpuIndex(0);

					for (int i = 1; i < _threads.Length - _hybridCoreDict.Count(item => item.Value == HybridCore.Efficiency); i++)
					{
						affinity |= GetBitMaskCpuIndex(i);
					}
				}
			}

			process.ProcessorAffinity = (IntPtr)affinity;
		}

		private bool IsHybridDesign()
		{
			// Alder Lake (Intel 7/10nm): 0x97, 0x9A
			// Raptor Lake (Intel 7/10nm): 0xB7
			// Raptor Lake (Alder Lake Refresh) (Intel 7/10nm): 0xBF
			return _vendor == Vendor.Intel && _family == 0x06
					&& (_model == 0x97 || _model == 0x9A || _model == 0xB7 || _model == 0xBF);
		}

		private long GetBitMaskCpuIndex(int i)
		{
			return 0x00000001L << i;
		}
	}
}
