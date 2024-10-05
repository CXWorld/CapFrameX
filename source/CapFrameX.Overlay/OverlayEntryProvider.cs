using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Hardware.Controller;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CapFrameX.Overlay
{
	public class OverlayEntryProvider : IOverlayEntryProvider
	{
		private static readonly string OVERLAY_CONFIG_FOLDER
			= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					@"CapFrameX\Configuration\");

		private static readonly HashSet<string> ONLINE_METRIC_NAMES = new HashSet<string>()
		{
			"OnlineAverage", "OnlineP1", "OnlineP0dot2", "Online1PercentLow", "Online0dot2PercentLow",
            "OnlineGpuActiveTimeAverage", "OnlineFrameTimeAverage", "OnlineGpuActiveTimePercentageDeviation",
			"OnlineStutteringPercentage", "PmdGpuPowerCurrent", "PmdCpuPowerCurrent", "PmdSystemPowerCurrent"
		};

		private readonly ISensorService _sensorService;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IEventAggregator _eventAggregator;
		private readonly IOnlineMetricService _onlineMetricService;
		private readonly ISystemInfo _systemInfo;
		private readonly IRTSSService _rTSSService;
		private readonly ISensorConfig _sensorConfig;
		private readonly IOverlayEntryCore _overlayEntryCore;
		private readonly IThreadAffinityController _threadAffinityController;
		private readonly IFrameViewService _frameViewService;

		private readonly ILogger<OverlayEntryProvider> _logger;
		private readonly ConcurrentDictionary<string, IOverlayEntry> _identifierOverlayEntryDict
			 = new ConcurrentDictionary<string, IOverlayEntry>();
		private readonly TaskCompletionSource<bool> _taskCompletionSource
			= new TaskCompletionSource<bool>();
		private readonly ConcurrentDictionary<string, int> _colorIndexDictionary
			= new ConcurrentDictionary<string, int>();
		private readonly ConcurrentDictionary<int, int> _sizeIndexDictionary
			= new ConcurrentDictionary<int, int>();

		private BlockingCollection<IOverlayEntry> _overlayEntries;
		private double _ping = double.NaN;
		private int _currentProcessId;

        private bool _showFramerateGraphSave;
        private bool _showFrametimeGraphSave;

        public bool HasHardwareChanged { get; set; }
		public bool ShowSystemTimeSeconds { get; set; }

		public OverlayEntryProvider(ISensorService sensorService,
			IAppConfiguration appConfiguration,
			IEventAggregator eventAggregator,
			IOnlineMetricService onlineMetricService,
			ISystemInfo systemInfo,
			IRTSSService rTSSService,
			ISensorConfig sensorConfig,
			IOverlayEntryCore overlayEntryCore,
			IThreadAffinityController threadAffinityController,
			IFrameViewService frameViewService,
			ILogger<OverlayEntryProvider> logger)
		{
			_sensorService = sensorService;
			_appConfiguration = appConfiguration;
			_eventAggregator = eventAggregator;
			_onlineMetricService = onlineMetricService;
			_systemInfo = systemInfo;
			_rTSSService = rTSSService;
			_sensorConfig = sensorConfig;
			_overlayEntryCore = overlayEntryCore;
			_threadAffinityController = threadAffinityController;
			_frameViewService = frameViewService;
			_logger = logger;

			_ = Task.Run(async () => await LoadOrSetDefault())
				.ContinueWith(task => _taskCompletionSource.SetResult(true));

			SubscribeToOptionPopupClosed();

			ShowSystemTimeSeconds = _appConfiguration.ShowSystemTimeSeconds;
			_appConfiguration.OnValueChanged
			.Where(x => x.key == nameof(IAppConfiguration.ShowSystemTimeSeconds))
			.Select(x => x.value)
			.Subscribe(value => ShowSystemTimeSeconds = (bool)value);

			rTSSService.ProcessIdStream.Subscribe(id =>
			{
				// update process ID
				_currentProcessId = id;
			});

			_logger.LogDebug("{componentName} Ready", this.GetType().Name);

			// Add <S0=50>
			_sizeIndexDictionary.TryAdd(50, 0);
		}

		public async Task<IOverlayEntry[]> GetOverlayEntries(bool updateFormats = true)
		{
			await _taskCompletionSource.Task;
			await UpdateSensorData();
			UpdateOnlineMetrics();
			UpdateAppInfo();
			UpdateThreadAffinityState();
			UpdateNetworkPing();
			UpdatePCLatency();

			if (updateFormats)
			{
				UpdateFormatting();
			}
			return _overlayEntries.ToArray();
		}

		public IOverlayEntry GetOverlayEntry(string identifier)
		{
			_identifierOverlayEntryDict.TryGetValue(identifier, out IOverlayEntry entry);
			return entry;
		}

		public void MoveEntry(int sourceIndex, int targetIndex)
		{
			_overlayEntries.Move(sourceIndex, targetIndex);
		}

		public async Task SaveOverlayEntriesToJson(int targetConfig)
		{
			try
			{
				var persistence = new OverlayEntryPersistence()
				{
					OverlayEntries = _overlayEntries.Select(entry => entry as OverlayEntryWrapper).ToList()
				};

				var json = JsonConvert.SerializeObject(persistence);

				if (!Directory.Exists(OVERLAY_CONFIG_FOLDER))
					Directory.CreateDirectory(OVERLAY_CONFIG_FOLDER);

				using (StreamWriter outputFile = new StreamWriter(GetConfigurationFileName(targetConfig)))
				{
					await outputFile.WriteAsync(json);
				}
			}
			catch { return; }
		}

		public async Task SwitchConfigurationTo(int index)
		{
			SetConfigurationFileName(index);
			await LoadOrSetDefault();
		}

		public async Task<IEnumerable<IOverlayEntry>> GetDefaultOverlayEntries()
		{
			_overlayEntries = await GetOverlayEntryDefaults();
			_identifierOverlayEntryDict.Clear();
			_sensorConfig.ResetEvaluate();
			foreach (var entry in _overlayEntries)
			{
				entry.UpdateShowOnOverlay = UpdateSensorIsActive;
				_sensorConfig.SetSensorEvaluate(entry.Identifier, entry.ShowOnOverlay);
				_identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);
			}

			ManageFormats();

			return _overlayEntries.ToList();
		}

		public void SetFormatForGroupName(string groupName, IOverlayEntry selectedEntry, IOverlayEntryFormatChange checkboxes)
		{
			foreach (var entry in _overlayEntries
					.Where(x => x.GroupName == groupName))
			{
				if (checkboxes.Colors)
				{
					entry.GroupColor = selectedEntry.GroupColor;
					entry.Color = selectedEntry.Color;
					entry.UpperLimitColor = selectedEntry.UpperLimitColor;
					entry.LowerLimitColor = selectedEntry.LowerLimitColor;
				}
				if (checkboxes.Limits)
				{
					entry.UpperLimitValue = selectedEntry.UpperLimitValue;
					entry.LowerLimitValue = selectedEntry.LowerLimitValue;
				}
				if (checkboxes.Format)
				{
					entry.GroupFontSize = selectedEntry.GroupFontSize;
					entry.ValueFontSize = selectedEntry.ValueFontSize;
				}
				entry.FormatChanged = true;
			}
		}

		public void SetFormatForSensorType(string sensorType, IOverlayEntry selectedEntry, IOverlayEntryFormatChange checkboxes)
		{
			foreach (var entry in _overlayEntries
					.Where(x => _sensorService.GetSensorTypeString(x.Identifier) == sensorType))
			{
				if (checkboxes.Colors)
				{
					entry.GroupColor = selectedEntry.GroupColor;
					entry.Color = selectedEntry.Color;
					entry.UpperLimitColor = selectedEntry.UpperLimitColor;
					entry.LowerLimitColor = selectedEntry.LowerLimitColor;
				}
				if (checkboxes.Limits)
				{
					entry.UpperLimitValue = selectedEntry.UpperLimitValue;
					entry.LowerLimitValue = selectedEntry.LowerLimitValue;
				}
				if (checkboxes.Format)
				{
					entry.GroupFontSize = selectedEntry.GroupFontSize;
					entry.ValueFontSize = selectedEntry.ValueFontSize;
				}
				entry.FormatChanged = true;
			}
		}

		public void ResetColorAndLimits(IOverlayEntry selectedEntry)
		{
			selectedEntry.UpperLimitValue = string.Empty;
			selectedEntry.LowerLimitValue = string.Empty;
			selectedEntry.GroupColor = string.Empty;
			selectedEntry.Color = string.Empty;
			selectedEntry.UpperLimitColor = string.Empty;
			selectedEntry.LowerLimitColor = string.Empty;
			selectedEntry.FormatChanged = true;
		}

		public async Task LoadOrSetDefault()
		{
			try
			{
				_overlayEntries = await GetInitializedOverlayEntryDictionary();
			}
			catch
			{
				_overlayEntries = await GetOverlayEntryDefaults();
			}

			_identifierOverlayEntryDict.Clear();
			_sensorConfig.IsInitialized = true;
			foreach (var entry in _overlayEntries)
			{
				entry.UpdateShowOnOverlay = UpdateSensorIsActive;
				_sensorConfig.SetSensorEvaluate(entry.Identifier, entry.ShowOnOverlay);
				_identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);

				if (ONLINE_METRIC_NAMES.Contains(entry.Identifier) || entry.Identifier == "SystemTime"
					|| entry.Identifier == "BatteryLifePercent" || entry.Identifier == "BatteryLifeRemaining"
					|| entry.Identifier == "Ping" || entry.Identifier == "ThreadAffinityState" || entry.Identifier == "PCLatency")
				{
					if (!_overlayEntryCore.RealtimeMetricEntryDict.ContainsKey(entry.Identifier))
						_overlayEntryCore.RealtimeMetricEntryDict.Add(entry.Identifier, entry);
					else
						_overlayEntryCore.RealtimeMetricEntryDict[entry.Identifier] = entry;
				}
			}

			CheckCustomSystemInfo();
			CheckOSVersion();
			CheckGpuDriver();

			ManageFormats();
		}

		private void UpdateSensorIsActive(string identifier, bool isShownOnOverlay)
		{
			if (identifier == null)
				return;
			_sensorConfig.SetSensorEvaluate(identifier, isShownOnOverlay);
		}

		private void ManageFormats()
		{
			// copy formats from sensor service
			_overlayEntries.ForEach(entry =>
				entry.ValueUnitFormat = GetSensorOverlayEntry(entry.Identifier)?.ValueUnitFormat);
			_overlayEntries.ForEach(entry =>
				entry.ValueAlignmentAndDigits = GetSensorOverlayEntry(entry.Identifier)?.ValueAlignmentAndDigits);
			SetOnlineMetricFormats();
			SetOnlineMetricsIsNumericState();
			SetRTSSMetricFormats();
			SetRTSSMetricIsNumericState();
			SetHardwareIsNumericState();
			SetAppInfoFormats();
			SetAppInfoIsNumericState();
			SetBatteryInfoFormats();
			SetBatteryInfoIsNumericState();
			SetNetworkPingIsNumericState();
			SetPCLatencyIsNumericState();
			SetPCLatencyFormats();

			_overlayEntries.ForEach(entry => entry.FormatChanged = true);
		}

		private async Task<BlockingCollection<IOverlayEntry>> GetInitializedOverlayEntryDictionary()
		{
			await _sensorService.SensorServiceCompletionSource.Task;
			await _overlayEntryCore.OverlayEntryCoreCompletionSource.Task;

			string json = File.ReadAllText(GetConfigurationFileName(_appConfiguration.OverlayEntryConfigurationFile));
			var overlayEntriesFromJson = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json)
				.OverlayEntries.ToBlockingCollection<IOverlayEntry>();

			var sensorOverlayEntryClones = _overlayEntryCore.OverlayEntryDict.Values.Select(entry => entry.Clone()).ToList();
			var sensorOverlayEntryDescriptions = sensorOverlayEntryClones
				.Select(entry => entry.Description)
				.ToList();
			var sensorGpuOverlayEntryDescriptions = GetOverlayentries(sensorOverlayEntryClones, EOverlayEntryType.GPU);
			var sensorCpuOverlayEntryDescriptions = GetOverlayentries(sensorOverlayEntryClones, EOverlayEntryType.CPU);
			var sensorRamOverlayEntryDescriptions = GetOverlayentries(sensorOverlayEntryClones, EOverlayEntryType.RAM);


			var configOverlayEntries = new List<IOverlayEntry>(overlayEntriesFromJson);

			var configOverlayEntryDescriptions = configOverlayEntries
				.Select(entry => entry.Description)
				.ToList();
			var configGpuOverlayEntryDescriptions = GetOverlayentries(configOverlayEntries, EOverlayEntryType.GPU);
			var configCpuOverlayEntryDescriptions = GetOverlayentries(configOverlayEntries, EOverlayEntryType.CPU);
			var configRamOverlayEntryDescriptions = GetOverlayentries(configOverlayEntries, EOverlayEntryType.RAM);


			List<string> GetOverlayentries(List<IOverlayEntry> Clones, EOverlayEntryType type)
			{
				return Clones
				.Where(entry => entry.OverlayEntryType == type)
				.Select(entry => entry.Description)
				.ToList();
			}

			bool hasGpuChanged = !sensorGpuOverlayEntryDescriptions.IsEquivalent(configGpuOverlayEntryDescriptions);
			bool hasCpuChanged = !sensorCpuOverlayEntryDescriptions.IsEquivalent(configCpuOverlayEntryDescriptions);
			bool hasRamChanged = !sensorRamOverlayEntryDescriptions.IsEquivalent(configRamOverlayEntryDescriptions);
			HasHardwareChanged = hasGpuChanged || hasCpuChanged || hasRamChanged;

			if (HasHardwareChanged)
			{
				for (int i = 0; i < sensorOverlayEntryDescriptions.Count; i++)
				{
					if (configOverlayEntryDescriptions.Contains(sensorOverlayEntryDescriptions[i]))
					{
						var configEntry = configOverlayEntries
							.Find(entry => entry.Description == sensorOverlayEntryDescriptions[i]);

						if (configEntry != null)
						{
							sensorOverlayEntryClones[i].ShowOnOverlay = configEntry.ShowOnOverlay;
							sensorOverlayEntryClones[i].ShowGraph = configEntry.ShowGraph;
							sensorOverlayEntryClones[i].Color = configEntry.Color;
							sensorOverlayEntryClones[i].ValueFontSize = configEntry.ValueFontSize;
							sensorOverlayEntryClones[i].UpperLimitValue = configEntry.UpperLimitValue;
							sensorOverlayEntryClones[i].LowerLimitValue = configEntry.LowerLimitValue;
							sensorOverlayEntryClones[i].GroupColor = configEntry.GroupColor;
							sensorOverlayEntryClones[i].GroupFontSize = configEntry.GroupFontSize;
							sensorOverlayEntryClones[i].GroupSeparators = configEntry.GroupSeparators;
							sensorOverlayEntryClones[i].UpperLimitColor = configEntry.UpperLimitColor;
							sensorOverlayEntryClones[i].LowerLimitColor = configEntry.LowerLimitColor;

							if (!sensorOverlayEntryClones[i].Description.Contains("CPU Core"))
								sensorOverlayEntryClones[i].GroupName = configEntry.GroupName;
						}
					}
				}
			}

			// check GPU changed 
			if (hasGpuChanged)
			{
				_logger.LogInformation("GPU changed. Config has to be updated.");
				InsertSensorEntries(EOverlayEntryType.GPU);
			}

			// check CPU changed 
			if (hasCpuChanged)
			{
				_logger.LogInformation("CPU changed. Config has to be updated.");
				InsertSensorEntries(EOverlayEntryType.CPU);
			}

			// check RAM changed
			if (hasRamChanged)
			{
				_logger.LogInformation("RAM. Config has to be updated.");
				InsertSensorEntries(EOverlayEntryType.RAM);
			}

			void InsertSensorEntries(EOverlayEntryType type)
			{
				var index = configOverlayEntries
					.TakeWhile(entry => entry.OverlayEntryType != type)
					.Count();

				configOverlayEntries = configOverlayEntries
					.Where(entry => entry.OverlayEntryType != type)
					.ToList();

				configOverlayEntries
					.InsertRange(index, sensorOverlayEntryClones.Where(entry => entry.OverlayEntryType == type));
			}

			// check separators
			var separatorDict = new Dictionary<string, int>();

			foreach (var entry in configOverlayEntries)
			{
				if (!separatorDict.ContainsKey(entry.GroupName))
					separatorDict.Add(entry.GroupName, entry.GroupSeparators);
				else
					separatorDict[entry.GroupName] = Math.Max(entry.GroupSeparators, separatorDict[entry.GroupName]);
			}

			foreach (var entry in configOverlayEntries)
			{
				entry.GroupSeparators = separatorDict[entry.GroupName];
			}

			// Manage default entries from Utils list
			var utilsDefaults = OverlayUtils.GetOverlayEntryDefaults();

			foreach (var defaultEntry in utilsDefaults)
			{
				if (configOverlayEntries.FirstOrDefault(entry => entry.Identifier == defaultEntry.Identifier) == null)
				{
					int index = utilsDefaults.IndexOf(defaultEntry) - 1;

					if (index >= 0)
					{
						var predecessorEntry = utilsDefaults[index];
						var predecessorConfigOverlayEntry = configOverlayEntries.FirstOrDefault(entry => entry.Identifier == predecessorEntry.Identifier);
						int predecessorConfigOverlayEntryIndex = configOverlayEntries.IndexOf(predecessorConfigOverlayEntry);
						configOverlayEntries.Insert(predecessorConfigOverlayEntryIndex + 1, defaultEntry);
					}
				}
			}

			return configOverlayEntries.ToBlockingCollection();
		}

		private void CheckOSVersion()
		{
			_identifierOverlayEntryDict.TryGetValue("OS", out IOverlayEntry entry);

			if (entry != null)
			{
				entry.Value = _systemInfo.GetOSVersion();
			}
		}

		private void CheckGpuDriver()
		{
			_identifierOverlayEntryDict.TryGetValue("GPUDriver", out IOverlayEntry entry);

			if (entry != null)
			{
				entry.Value = _sensorService.GetGpuDriverVersion();
			}
		}

		private void CheckCustomSystemInfo()
		{
			_identifierOverlayEntryDict.TryGetValue("CustomCPU", out IOverlayEntry customCPUEntry);

			if (customCPUEntry != null)
			{
				customCPUEntry.Value =
					_appConfiguration.HardwareInfoSource == "Auto" ? _systemInfo.GetProcessorName()
					: _appConfiguration.CustomCpuDescription;
			}

			_identifierOverlayEntryDict.TryGetValue("CustomGPU", out IOverlayEntry customGPUEntry);

			if (customGPUEntry != null)
			{
				customGPUEntry.Value =
					_appConfiguration.HardwareInfoSource == "Auto" ? _systemInfo.GetGraphicCardName()
					: _appConfiguration.CustomGpuDescription;
			}

			_identifierOverlayEntryDict.TryGetValue("Mainboard", out IOverlayEntry mainboardEntry);

			if (mainboardEntry != null)
			{
				mainboardEntry.Value = _systemInfo.GetMotherboardName();
			}

			_identifierOverlayEntryDict.TryGetValue("CustomRAM", out IOverlayEntry customRAMEntry); ;

			if (customRAMEntry != null)
			{
				customRAMEntry.Value =
					_appConfiguration.HardwareInfoSource == "Auto" ? _systemInfo.GetSystemRAMInfoName()
					: _appConfiguration.CustomRamDescription;
			}
		}

		private async Task<BlockingCollection<IOverlayEntry>> GetOverlayEntryDefaults()
		{
			var overlayEntries = OverlayUtils.GetOverlayEntryDefaults()
					.Select(item => (item as IOverlayEntry).Clone()).ToBlockingCollection();

			//log hardware configs
			_logger.LogInformation("Set overlay defaults");
			_logger.LogInformation("CPU detected: {cpuName}.", _sensorService.GetCpuName());
			_logger.LogInformation("CPU threads detected: {threadCount}.", Environment.ProcessorCount);
			_logger.LogInformation("GPU detected: {gpuName}.", _sensorService.GetGpuName());

			await _overlayEntryCore.OverlayEntryCoreCompletionSource.Task;

			// Sensor data
			_overlayEntryCore.OverlayEntryDict.Values.ForEach(sensor => overlayEntries.TryAdd(sensor.Clone()));
			return await Task.FromResult(overlayEntries);
		}

		private async Task UpdateSensorData()
		{
			var currentFramerate = _rTSSService.GetCurrentFramerate(await _rTSSService.ProcessIdStream.Take(1));

			foreach (var entry in _overlayEntries)
			{
				switch (entry.OverlayEntryType)
				{
					case EOverlayEntryType.GPU:
					case EOverlayEntryType.CPU:
					case EOverlayEntryType.RAM:
						entry.Value = GetSensorOverlayEntry(entry.Identifier)?.Value;
						break;
					case EOverlayEntryType.CX when entry.Identifier == "Framerate":
						entry.Value = currentFramerate.Item1;
                        break;
					case EOverlayEntryType.CX when entry.Identifier == "Frametime":
						entry.Value = currentFramerate.Item2;
                        break;
					case EOverlayEntryType.CX when entry.Identifier == "SystemTime":
						entry.Value = ShowSystemTimeSeconds ? DateTime.Now.ToString("HH:mm:ss") : DateTime.Now.ToString("HH:mm");
						break;
					case EOverlayEntryType.CX when entry.Identifier == "BatteryLifePercent":
						entry.Value = SystemInformation.PowerStatus.BatteryLifePercent * 100d;
						break;
					case EOverlayEntryType.CX when entry.Identifier == "BatteryLifeRemaining":
						entry.Value = SystemInformation.PowerStatus.BatteryLifeRemaining / 60d;
						break;
					default:
						break;
				}
			}
		}

		private IOverlayEntry GetSensorOverlayEntry(string identifier)
		{
			_overlayEntryCore.OverlayEntryDict.TryGetValue(identifier, out IOverlayEntry entry);
			return entry;
		}

		private void UpdateOnlineMetrics()
		{
			// average
			_identifierOverlayEntryDict.TryGetValue("OnlineAverage", out IOverlayEntry averageEntry);

			if (averageEntry != null && averageEntry.ShowOnOverlay)
			{
				averageEntry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.Average));
			}

			// P1
			_identifierOverlayEntryDict.TryGetValue("OnlineP1", out IOverlayEntry p1Entry);

			if (p1Entry != null && p1Entry.ShowOnOverlay)
			{
				p1Entry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.P1));
			}

			// P0.2
			_identifierOverlayEntryDict.TryGetValue("OnlineP0dot2", out IOverlayEntry p1dot2Entry);

			if (p1dot2Entry != null && p1dot2Entry.ShowOnOverlay)
			{
				p1dot2Entry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.P0dot2));
			}

			// 1% Low
			_identifierOverlayEntryDict.TryGetValue("Online1PercentLow", out IOverlayEntry onePercentLowEntry);

			if (onePercentLowEntry != null && onePercentLowEntry.ShowOnOverlay)
			{
				onePercentLowEntry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.OnePercentLowAverage));
			}

			// 0.2% Low
			_identifierOverlayEntryDict.TryGetValue("Online0dot2PercentLow", out IOverlayEntry zeroDotTwoPercentLowEntry);

			if (zeroDotTwoPercentLowEntry != null && zeroDotTwoPercentLowEntry.ShowOnOverlay)
			{
				zeroDotTwoPercentLowEntry.Value = Math.Round(_onlineMetricService.GetOnlineFpsMetricValue(EMetric.ZerodotTwoPercentLowAverage));
			}

            // GPU Active Time Average
            _identifierOverlayEntryDict.TryGetValue("OnlineGpuActiveTimeAverage", out IOverlayEntry gpuActiveTimeAverage);

			if (gpuActiveTimeAverage != null && gpuActiveTimeAverage.ShowOnOverlay)
			{
                gpuActiveTimeAverage.Value = Math.Round(_onlineMetricService.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveAverage), 1);
			}

            // Frame Time Average
            _identifierOverlayEntryDict.TryGetValue("OnlineFrameTimeAverage", out IOverlayEntry frameTimeAverage);

			if (frameTimeAverage != null && frameTimeAverage.ShowOnOverlay)
			{
                frameTimeAverage.Value = Math.Round(_onlineMetricService.GetOnlineFrameTimeMetricValue(EMetric.Average), 1);
			}

            // GPU Active Time Deviation
            _identifierOverlayEntryDict.TryGetValue("OnlineGpuActiveTimePercentageDeviation", out IOverlayEntry gpuActiveTimeDeviation);

			if (gpuActiveTimeDeviation != null && gpuActiveTimeDeviation.ShowOnOverlay)
			{
                gpuActiveTimeDeviation.Value = Math.Round(_onlineMetricService.GetOnlineGpuActiveTimeDeviationMetricValue());
			}

			// stuttering percentage (time)
			_identifierOverlayEntryDict.TryGetValue("OnlineStutteringPercentage", out IOverlayEntry stutteringPercentage);

			if (stutteringPercentage != null && stutteringPercentage.ShowOnOverlay)
			{
				stutteringPercentage.Value = Math.Round(_onlineMetricService.GetOnlineStutteringPercentageValue(), 1);
			}

			// PMD metrics
			_identifierOverlayEntryDict.TryGetValue("PmdGpuPowerCurrent", out IOverlayEntry pmdGpuPowerCurrent);
			_identifierOverlayEntryDict.TryGetValue("PmdCpuPowerCurrent", out IOverlayEntry pmdcpuPowerCurrent);
			_identifierOverlayEntryDict.TryGetValue("PmdSystemPowerCurrent", out IOverlayEntry pmdSystemPowerCurrent);

			if ((pmdGpuPowerCurrent != null && pmdGpuPowerCurrent.ShowOnOverlay)
				|| (pmdcpuPowerCurrent != null && pmdcpuPowerCurrent.ShowOnOverlay)
				|| (pmdSystemPowerCurrent != null && pmdSystemPowerCurrent.ShowOnOverlay))
			{
				OnlinePmdMetrics pmdMetrics = _onlineMetricService.GetPmdMetricsPowerCurrent();
				if (pmdMetrics != null)
				{
					if (pmdGpuPowerCurrent != null && pmdGpuPowerCurrent.ShowOnOverlay)
					{
						pmdGpuPowerCurrent.Value = Math.Round(pmdMetrics.GpuPowerCurrent, 1);
					}

					if (pmdcpuPowerCurrent != null && pmdcpuPowerCurrent.ShowOnOverlay)
					{
						pmdcpuPowerCurrent.Value = Math.Round(pmdMetrics.CpuPowerCurrent, 1);
					}

					if (pmdSystemPowerCurrent != null && pmdSystemPowerCurrent.ShowOnOverlay)
					{
						pmdSystemPowerCurrent.Value = Math.Round(pmdMetrics.SystemPowerCurrent, 1);
					}
				}
			}
		}

		private void UpdateAppInfo()
		{
			// CX CPU usage
			_identifierOverlayEntryDict.TryGetValue("CxAppCpuUsage", out IOverlayEntry cxCpuUsage);

			if (cxCpuUsage != null)
			{
				cxCpuUsage.Value = _systemInfo.GetCapFrameXAppCpuUsage();
			}
		}

		private void UpdateThreadAffinityState()
		{
			_identifierOverlayEntryDict.TryGetValue("ThreadAffinityState", out IOverlayEntry threadAffinityState);

			if (threadAffinityState != null && threadAffinityState.ShowOnOverlay)
			{
				var threadAffinityStateText = _threadAffinityController.CpuAffinityState == AffinityState.ECores ? "E-Cores"
					: _threadAffinityController.CpuAffinityState == AffinityState.PCores ? "P-Cores"
					: _threadAffinityController.CpuAffinityState.ToString();

				threadAffinityState.Value = threadAffinityStateText;
			}
		}

		private void UpdateNetworkPing()
		{
			_identifierOverlayEntryDict.TryGetValue("Ping", out IOverlayEntry ping);

			if (ping != null && ping.ShowOnOverlay)
			{
				ping.Value = Math.Round(_ping, 0);
				SetPing();
			}
		}

		private void UpdatePCLatency()
		{
			_identifierOverlayEntryDict.TryGetValue("PCLatency", out IOverlayEntry pcLatency);

			if (pcLatency != null && pcLatency.ShowOnOverlay)
			{
				pcLatency.Value = _frameViewService.GetAveragePcLatency(_currentProcessId);
			}
		}

		private void SetOnlineMetricsIsNumericState()
		{
			foreach (var metricName in ONLINE_METRIC_NAMES)
			{
				_identifierOverlayEntryDict.TryGetValue(metricName, out IOverlayEntry metricEntry);

				if (metricEntry != null)
				{
					metricEntry.IsNumeric = true;
				}
			}
		}

		private void SetOnlineMetricFormats()
		{
			// average
			_identifierOverlayEntryDict.TryGetValue("OnlineAverage", out IOverlayEntry averageEntry);

			if (averageEntry != null)
			{
				averageEntry.ValueUnitFormat = "FPS";
				averageEntry.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// P1
			_identifierOverlayEntryDict.TryGetValue("OnlineP1", out IOverlayEntry p1Entry);

			if (p1Entry != null)
			{
				p1Entry.ValueUnitFormat = "FPS";
				p1Entry.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// P0.2
			_identifierOverlayEntryDict.TryGetValue("OnlineP0dot2", out IOverlayEntry p1dot2Entry);

			if (p1dot2Entry != null)
			{
				p1dot2Entry.ValueUnitFormat = "FPS";
				p1dot2Entry.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// 1% Low
			_identifierOverlayEntryDict.TryGetValue("Online1PercentLow", out IOverlayEntry onePercentLowEntry);

			if (onePercentLowEntry != null)
			{
				onePercentLowEntry.ValueUnitFormat = "FPS";
				onePercentLowEntry.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// 0.2% Low
			_identifierOverlayEntryDict.TryGetValue("Online0dot2PercentLow", out IOverlayEntry zeroDotTwoPercentLowEntry);

			if (zeroDotTwoPercentLowEntry != null)
			{
				zeroDotTwoPercentLowEntry.ValueUnitFormat = "FPS";
				zeroDotTwoPercentLowEntry.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// GPU Active Time Average
			_identifierOverlayEntryDict.TryGetValue("OnlineGpuActiveTimeAverage", out IOverlayEntry gpuActiveTimeAverage);

			if (gpuActiveTimeAverage != null)
			{
                gpuActiveTimeAverage.ValueUnitFormat = "ms";
                gpuActiveTimeAverage.ValueAlignmentAndDigits = "{0,5:F1}";
			}

			// Frame Time Average
			_identifierOverlayEntryDict.TryGetValue("OnlineFrameTimeAverage", out IOverlayEntry frameTimeAverage);

			if (frameTimeAverage != null)
			{
                frameTimeAverage.ValueUnitFormat = "ms";
                frameTimeAverage.ValueAlignmentAndDigits = "{0,5:F1}";
			}

			// GPU Active Time Deviation
			_identifierOverlayEntryDict.TryGetValue("OnlineGpuActiveTimePercentageDeviation", out IOverlayEntry gpuActiveTimeDeviation);

			if (gpuActiveTimeDeviation != null)
			{
                gpuActiveTimeDeviation.ValueUnitFormat = "%";
                gpuActiveTimeDeviation.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// stuttering percentage
			_identifierOverlayEntryDict.TryGetValue("OnlineStutteringPercentage", out IOverlayEntry stutteringPercentage);

			if (stutteringPercentage != null)
			{
				stutteringPercentage.ValueUnitFormat = "%";
				stutteringPercentage.ValueAlignmentAndDigits = "{0,5:F1}";
			}

			// ping
			_identifierOverlayEntryDict.TryGetValue("Ping", out IOverlayEntry ping);

			if (ping != null)
			{
				ping.ValueUnitFormat = "ms";
				ping.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// PMD
			var pmdMetrics = new List<string>()
			{
				"PmdGpuPowerCurrent", "PmdCpuPowerCurrent", "PmdSystemPowerCurrent"
			};

			foreach (var pmdMetric in pmdMetrics)
			{
				_identifierOverlayEntryDict.TryGetValue(pmdMetric, out IOverlayEntry pmdMetricEntry);

				if (pmdMetricEntry != null)
				{
					pmdMetricEntry.ValueUnitFormat = "W";
					pmdMetricEntry.ValueAlignmentAndDigits = "{0,5:F1}";
				}
			}
		}

		private void SetRTSSMetricIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
				x.Identifier == "Framerate" || x.Identifier == "Frametime"))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetAppInfoFormats()
		{
			// CPU usage
			_identifierOverlayEntryDict.TryGetValue("CxAppCpuUsage", out IOverlayEntry cxCpuUsage);

			if (cxCpuUsage != null)
			{
				cxCpuUsage.ValueUnitFormat = "%";
				cxCpuUsage.ValueAlignmentAndDigits = "{0,5:F1}";
			}
		}

		private void SetAppInfoIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x => x.Identifier == "CxAppCpuUsage"))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetRTSSMetricFormats()
		{
			// framerate
			_identifierOverlayEntryDict.TryGetValue("Framerate", out IOverlayEntry framerateEntry);

			if (framerateEntry != null)
			{
				framerateEntry.ValueUnitFormat = "FPS";
				framerateEntry.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// frametime
			_identifierOverlayEntryDict.TryGetValue("Frametime", out IOverlayEntry frametimeEntry);

			if (frametimeEntry != null)
			{
				frametimeEntry.ValueUnitFormat = "ms ";
				frametimeEntry.ValueAlignmentAndDigits = "{0,5:F1}";
			}
		}

		private void SetHardwareIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
			   x.OverlayEntryType == EOverlayEntryType.GPU
				|| x.OverlayEntryType == EOverlayEntryType.CPU
				|| x.OverlayEntryType == EOverlayEntryType.RAM))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetBatteryInfoIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
				x.Identifier == "BatteryLifePercent" || x.Identifier == "BatteryLifeRemaining"))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetNetworkPingIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
			   x.Identifier == "Ping"))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetPCLatencyIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
			   x.Identifier == "PCLatency"))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetPCLatencyFormats()
		{
			// PC latency
			_identifierOverlayEntryDict.TryGetValue("PCLatency", out IOverlayEntry pcLatency);

			if (pcLatency != null)
			{
				pcLatency.ValueUnitFormat = "ms";
				pcLatency.ValueAlignmentAndDigits = "{0,5:F1}";
			}
		}

		private void SetBatteryInfoFormats()
		{
			// BatteryLifePercent
			_identifierOverlayEntryDict.TryGetValue("BatteryLifePercent", out IOverlayEntry batteryLifePercent);

			if (batteryLifePercent != null)
			{
				batteryLifePercent.ValueUnitFormat = "%";
				batteryLifePercent.ValueAlignmentAndDigits = "{0,5:F0}";
			}

			// BatteryLifeRemaining
			_identifierOverlayEntryDict.TryGetValue("BatteryLifeRemaining", out IOverlayEntry batteryLifeRemaining);

			if (batteryLifeRemaining != null)
			{
				batteryLifeRemaining.ValueUnitFormat = "min ";
				batteryLifeRemaining.ValueAlignmentAndDigits = "{0,5:F0}";
			}
		}

		private void UpdateFormatting()
		{
			bool updateVariables = false;

			foreach (var entry in _overlayEntries)
			{
				if (entry.FormatChanged)
				{
					updateVariables = true;

					// group name format
					var basicGroupFormat = entry.GroupSeparators == 0 ? "{0}"
						: Enumerable.Repeat("\n", entry.GroupSeparators).Aggregate((i, j) => i + j) + "{0}";
					var groupNameFormatStringBuilder = new StringBuilder();
					// groupNameFormatStringBuilder.Append("<S=");
					AppendSizeFormat(groupNameFormatStringBuilder, entry.GroupFontSize);
					// groupNameFormatStringBuilder.Append(entry.GroupFontSize.ToString());
					AppendColorFormat(groupNameFormatStringBuilder, entry.GroupColor);
					groupNameFormatStringBuilder.Append(basicGroupFormat);
					groupNameFormatStringBuilder.Append(" <C><S>");
					entry.GroupNameFormat = groupNameFormatStringBuilder.ToString();

					if (entry.ValueUnitFormat != null && entry.ValueAlignmentAndDigits != null)
					{
						var valueFormatStringBuilder = new StringBuilder();
						// valueFormatStringBuilder.Append("<S=");
						AppendSizeFormat(valueFormatStringBuilder, entry.ValueFontSize);
						// valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
						AppendColorFormat(valueFormatStringBuilder, entry.Color);
						valueFormatStringBuilder.Append(entry.ValueAlignmentAndDigits);
						valueFormatStringBuilder.Append("<C><S>");
						// valueFormatStringBuilder.Append("<S=");
						AppendSizeFormat(valueFormatStringBuilder, entry.ValueFontSize / 2);
						// valueFormatStringBuilder.Append((entry.ValueFontSize / 2).ToString());
						AppendColorFormat(valueFormatStringBuilder, entry.Color);
						valueFormatStringBuilder.Append(entry.ValueUnitFormat);
						valueFormatStringBuilder.Append("<C><S>");
						entry.ValueFormat = valueFormatStringBuilder.ToString();
					}
					else
					{
						var valueFormatStringBuilder = new StringBuilder();
						//valueFormatStringBuilder.Append("<S=");
						AppendSizeFormat(valueFormatStringBuilder, entry.ValueFontSize);
						// valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
						AppendColorFormat(valueFormatStringBuilder, entry.Color);
						valueFormatStringBuilder.Append("{0}<C><S>");
						entry.ValueFormat = valueFormatStringBuilder.ToString();
					}

					// reset format changed  and last limit state 
					entry.FormatChanged = false;
					entry.LastLimitState = LimitState.Undefined;
				}

				// check value limits
				if (entry.ShowOnOverlay)
				{
					if (!(entry.LowerLimitValue == string.Empty && entry.UpperLimitValue == string.Empty))
					{
						var currentColor = string.Empty;
						bool upperLimit = false;
						bool lowerLimit = false;
						LimitState limitState = LimitState.Undefined;

						if (entry.Value == null)
							continue;

						if (entry.UpperLimitValue != string.Empty)
						{
							if (!double.TryParse(entry.Value.ToString(), out double currentConvertedValue))
								continue;

							if (!double.TryParse(entry.UpperLimitValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double convertedUpperValue))
								continue;

							if (currentConvertedValue >= convertedUpperValue)
							{
								currentColor = entry.UpperLimitColor;
								upperLimit = true;
								limitState = LimitState.Upper;
							}
						}

						if (entry.LowerLimitValue != string.Empty)
						{
							if (!upperLimit)
							{
								if (!double.TryParse(entry.Value.ToString(), out double currentConvertedValue))
									continue;

								if (!double.TryParse(entry.LowerLimitValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double convertedLowerValue))
									continue;

								if (currentConvertedValue <= convertedLowerValue)
								{
									currentColor = entry.LowerLimitColor;
									lowerLimit = true;
									limitState = LimitState.Lower;
								}
							}
						}

						if (!upperLimit && !lowerLimit)
						{
							currentColor = entry.Color;
							limitState = LimitState.None;
						}

						if (limitState != entry.LastLimitState)
						{
							if (entry.ValueUnitFormat != null && entry.ValueAlignmentAndDigits != null)
							{
								updateVariables = true;

								var valueFormatStringBuilder = new StringBuilder();
								// valueFormatStringBuilder.Append("<S=");
								AppendSizeFormat(valueFormatStringBuilder, entry.ValueFontSize);
								// valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
								AppendColorFormat(valueFormatStringBuilder, currentColor);
								valueFormatStringBuilder.Append(entry.ValueAlignmentAndDigits);
								valueFormatStringBuilder.Append("<C><S>");
								//valueFormatStringBuilder.Append("<S=");
								AppendSizeFormat(valueFormatStringBuilder, entry.ValueFontSize / 2);
								// valueFormatStringBuilder.Append((entry.ValueFontSize / 2).ToString());
								AppendColorFormat(valueFormatStringBuilder, currentColor);
								valueFormatStringBuilder.Append(entry.ValueUnitFormat);
								valueFormatStringBuilder.Append("<C><S>");
								entry.ValueFormat = valueFormatStringBuilder.ToString();
							}
							else
							{
								updateVariables = true;

								var valueFormatStringBuilder = new StringBuilder();
								//valueFormatStringBuilder.Append("<S=");
								AppendSizeFormat(valueFormatStringBuilder, entry.ValueFontSize);
								// valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
								AppendColorFormat(valueFormatStringBuilder, currentColor);
								valueFormatStringBuilder.Append("{0}<C><S>");
								entry.ValueFormat = valueFormatStringBuilder.ToString();
							}

							entry.LastLimitState = limitState;
						}
					}
				}
			}

			if (updateVariables)
			{
				var variablesStringBuilder = new StringBuilder();
				_colorIndexDictionary.ForEach(pair => variablesStringBuilder.Append($"<C{pair.Value}={pair.Key}>"));
				_sizeIndexDictionary.ForEach(pair => variablesStringBuilder.Append($"<S{pair.Value}={pair.Key}>"));
				_rTSSService.SetFormatVariables(variablesStringBuilder.ToString());
			}
		}

		private void AppendColorFormat(StringBuilder formatStringBuilder, string groupColor)
		{
			if (_colorIndexDictionary.TryGetValue(groupColor, out var value))
			{
				formatStringBuilder.Append($"><C{value}>");
			}
			else
			{
				int index = _colorIndexDictionary.Count() + 1;
				_colorIndexDictionary.TryAdd(groupColor, index);

				formatStringBuilder.Append($"><C{index}>");
			}
		}

		private void AppendSizeFormat(StringBuilder formatStringBuilder, int size)
		{
			if (_sizeIndexDictionary.TryGetValue(size, out var value))
			{
				formatStringBuilder.Append($"<S{value}");
			}
			else
			{
				int index = _sizeIndexDictionary.Count() + 1;
				_sizeIndexDictionary.TryAdd(size, index);

				formatStringBuilder.Append($"<S{index}");
			}
		}

		private string GetConfigurationFileName(int targetConfig)
		{
			return Path.Combine(OVERLAY_CONFIG_FOLDER, $"OverlayEntryConfiguration_" +
					$"{targetConfig}.json");
		}

		private void SetConfigurationFileName(int index)
		{
			_appConfiguration.OverlayEntryConfigurationFile = index;
		}

		private void SubscribeToOptionPopupClosed()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>()
				.Subscribe(_ =>
				{
					CheckCustomSystemInfo();
				});
		}

		private async void SetPing()
		{
			Ping pingSender = new Ping();
			try
			{
				await Task.Run(() =>
				{
					PingReply reply = pingSender.Send(_appConfiguration.PingURL);
					if (reply.Status == IPStatus.Success)
					{
						_ping = Convert.ToDouble(reply.RoundtripTime);
					}
					else
					{
						_ping = double.NaN;
					}
				});
			}
			catch
			{
				_ping = double.NaN;
			};
		}
	}
}
