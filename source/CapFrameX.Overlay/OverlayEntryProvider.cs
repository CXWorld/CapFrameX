using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
	public class OverlayEntryProvider : IOverlayEntryProvider
	{
		private static readonly string OVERLAY_CONFIG_FOLDER
			= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					@"CapFrameX\OverlayConfiguration\");

		private readonly ISensorService _sensorService;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IEventAggregator _eventAggregator;
		private readonly IOnlineMetricService _onlineMetricService;
		private readonly ISystemInfo _systemInfo;

		private readonly ConcurrentDictionary<string, IOverlayEntry> _identifierOverlayEntryDict
			 = new ConcurrentDictionary<string, IOverlayEntry>();
		private readonly TaskCompletionSource<bool> _taskCompletionSource
			= new TaskCompletionSource<bool>();
		private BlockingCollection<IOverlayEntry> _overlayEntries;
		private IObservable<IOverlayEntry[]> _onDictionaryUpdatedBuffered;

		public OverlayEntryProvider(ISensorService sensorService,
			IAppConfiguration appConfiguration,
			IEventAggregator eventAggregator,
			IOnlineMetricService onlineMetricService,
			ISystemInfo systemInfo)
		{
			_sensorService = sensorService;
			_appConfiguration = appConfiguration;
			_eventAggregator = eventAggregator;
			_onlineMetricService = onlineMetricService;
			_systemInfo = systemInfo;
			_onDictionaryUpdatedBuffered = _sensorService
				.OnDictionaryUpdated
				.Replay(1)
				.AutoConnect(0);

			_ = Task.Run(async () => await LoadOrSetDefault())
				.ContinueWith(task => _taskCompletionSource.SetResult(true));

			SubscribeToOptionPopupClosed();
		}

		public async Task<IOverlayEntry[]> GetOverlayEntries()
		{
			await _taskCompletionSource.Task;
			UpdateSensorData();
			UpdateOnlineMetrics();
			UpdateFormatting();
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

		public async Task SaveOverlayEntriesToJson()
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

				using (StreamWriter outputFile = new StreamWriter(GetConfigurationFileName()))
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
			_overlayEntries = await SetOverlayEntryDefaults();
			_identifierOverlayEntryDict.Clear();
			foreach (var entry in _overlayEntries)
			{
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
					.Where(x => _sensorService.GetSensorTypeString(x) == sensorType))
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

		private async Task LoadOrSetDefault()
		{
			try
			{
				_overlayEntries = await InitializeOverlayEntryDictionary();
			}
			catch
			{
				_overlayEntries = await SetOverlayEntryDefaults();
			}
			_identifierOverlayEntryDict.Clear();
			foreach (var entry in _overlayEntries)
			{
				_identifierOverlayEntryDict.TryAdd(entry.Identifier, entry);
			}
			CheckCustomSystemInfo();
			CheckOSVersion();
			CheckGpuDriver();

			ManageFormats();
		}

		private void ManageFormats()
		{
			// copy formats from sensor service
			_overlayEntries.ForEach(entry =>
				entry.ValueUnitFormat = _sensorService.GetSensorOverlayEntry(entry.Identifier)?.ValueUnitFormat);
			_overlayEntries.ForEach(entry =>
				entry.ValueAlignmentAndDigits = _sensorService.GetSensorOverlayEntry(entry.Identifier)?.ValueAlignmentAndDigits);
			SetOnlineMetricFormats();

			SetOnlineMetricsIsNumericState();
			//SetRTSSMetricIsNumericState();
			SetHardwareIsNumericState();
			_overlayEntries.ForEach(entry => entry.FormatChanged = true);
		}

		private IObservable<BlockingCollection<IOverlayEntry>> InitializeOverlayEntryDictionary()
		{
			string json = File.ReadAllText(GetConfigurationFileName());
			var overlayEntriesFromJson = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json)
				.OverlayEntries.ToBlockingCollection<IOverlayEntry>();

			return _onDictionaryUpdatedBuffered
				.Take(1)
				.Select(sensorOverlayEntries =>
				{
					var sensorOverlayEntryIdentfiers = sensorOverlayEntries
						.Select(entry => entry.Identifier)
						.ToList();
					var sensorGpuOverlayEntryIdentfiers = sensorOverlayEntries
						.Where(entry => entry.OverlayEntryType == EOverlayEntryType.GPU)
						.Select(entry => entry.Identifier)
						.ToList();
					var sensorCpuOverlayEntryIdentfiers = sensorOverlayEntries
						.Where(entry => entry.OverlayEntryType == EOverlayEntryType.CPU)
						.Select(entry => entry.Identifier)
						.ToList();

					var adjustedOverlayEntries = new List<IOverlayEntry>(overlayEntriesFromJson);
					var adjustedOverlayEntryIdentfiers = adjustedOverlayEntries
						.Select(entry => entry.Identifier)
						.ToList();
					var adjustedGpuOverlayEntryIdentfiers = adjustedOverlayEntries
						.Where(entry => entry.OverlayEntryType == EOverlayEntryType.GPU)
						.Select(entry => entry.Identifier)
						.ToList();
					var adjustedCpuOverlayEntryIdentfiers = adjustedOverlayEntries
						.Where(entry => entry.OverlayEntryType == EOverlayEntryType.CPU)
						.Select(entry => entry.Identifier)
						.ToList();

					bool hasGpuChanged = !sensorGpuOverlayEntryIdentfiers.IsEquivalent(adjustedGpuOverlayEntryIdentfiers);
					bool hasCpuChanged = !sensorCpuOverlayEntryIdentfiers.IsEquivalent(adjustedCpuOverlayEntryIdentfiers);

					if (hasGpuChanged || hasCpuChanged)
					{
						for (int i = 0; i < sensorOverlayEntryIdentfiers.Count; i++)
						{
							if (adjustedOverlayEntryIdentfiers.Contains(sensorOverlayEntryIdentfiers[i]))
							{
								var configEntry = adjustedOverlayEntries
									.Find(entry => entry.Identifier == sensorOverlayEntryIdentfiers[i]);
								sensorOverlayEntries[i].ShowOnOverlay = configEntry.ShowOnOverlay;
								sensorOverlayEntries[i].ShowGraph = configEntry.ShowGraph;
								sensorOverlayEntries[i].Color = configEntry.Color;
								sensorOverlayEntries[i].ValueFontSize = configEntry.ValueFontSize;
								sensorOverlayEntries[i].UpperLimitValue = configEntry.UpperLimitValue;
								sensorOverlayEntries[i].LowerLimitValue = configEntry.LowerLimitValue;
								sensorOverlayEntries[i].GroupColor = configEntry.GroupColor;
								sensorOverlayEntries[i].GroupFontSize = configEntry.GroupFontSize;
								sensorOverlayEntries[i].GroupSeparators = configEntry.GroupSeparators;
								sensorOverlayEntries[i].UpperLimitColor = configEntry.UpperLimitColor;
								sensorOverlayEntries[i].LowerLimitColor = configEntry.LowerLimitColor;
							}
						}
					}

					// check GPU changed 
					if (hasGpuChanged)
					{
						var indexGpu = adjustedOverlayEntries
							.TakeWhile(entry => entry.OverlayEntryType != EOverlayEntryType.GPU)
							.Count();

						adjustedOverlayEntries = adjustedOverlayEntries
							.Where(entry => entry.OverlayEntryType != EOverlayEntryType.GPU)
							.ToList();

						adjustedOverlayEntries
							.InsertRange(indexGpu, sensorOverlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.GPU));
					}

					// check CPU changed 
					if (hasCpuChanged)
					{
						var indexCpu = adjustedOverlayEntries
							.TakeWhile(entry => entry.OverlayEntryType != EOverlayEntryType.CPU)
							.Count();

						adjustedOverlayEntries = adjustedOverlayEntries
							.Where(entry => entry.OverlayEntryType != EOverlayEntryType.CPU)
							.ToList();

						adjustedOverlayEntries
							.InsertRange(indexCpu, sensorOverlayEntries.Where(entry => entry.OverlayEntryType == EOverlayEntryType.CPU));
					}

					return adjustedOverlayEntries.ToBlockingCollection();
				});
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

		private IObservable<BlockingCollection<IOverlayEntry>> SetOverlayEntryDefaults()
		{
			var overlayEntries = OverlayUtils.GetOverlayEntryDefaults()
					.Select(item => item as IOverlayEntry).ToBlockingCollection();

			_sensorService.ResetSensorOverlayEntries();

			// Sensor data
			return _onDictionaryUpdatedBuffered
				.Take(1)
				.Select(sensorOverlayEntries =>
				{
					sensorOverlayEntries.ForEach(sensor => overlayEntries.TryAdd(sensor));
					return overlayEntries;
				});
		}

		private void UpdateSensorData()
		{
			foreach (var entry in _overlayEntries.Where(x =>
				(x.OverlayEntryType == EOverlayEntryType.GPU
				 || x.OverlayEntryType == EOverlayEntryType.CPU
				 || x.OverlayEntryType == EOverlayEntryType.RAM)))
			{
				var sensorEntry = _sensorService.GetSensorOverlayEntry(entry.Identifier);
				entry.Value = sensorEntry?.Value;
			}
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
		}

		private void SetOnlineMetricsIsNumericState()
		{
			// average
			_identifierOverlayEntryDict.TryGetValue("OnlineAverage", out IOverlayEntry averageEntry);

			if (averageEntry != null)
			{
				averageEntry.IsNumeric = true;
			}

			// P1
			_identifierOverlayEntryDict.TryGetValue("OnlineP1", out IOverlayEntry p1Entry);

			if (p1Entry != null)
			{
				p1Entry.IsNumeric = true;
			}

			// P0.2
			_identifierOverlayEntryDict.TryGetValue("OnlineP0dot2", out IOverlayEntry p1dot2Entry);

			if (p1dot2Entry != null)
			{
				p1dot2Entry.IsNumeric = true;
			}
		}

		private void SetOnlineMetricFormats()
		{
			// average
			_identifierOverlayEntryDict.TryGetValue("OnlineAverage", out IOverlayEntry averageEntry);

			if (averageEntry != null)
			{
				averageEntry.ValueUnitFormat = "FPS";
				averageEntry.ValueAlignmentAndDigits = "{0,4:F0}";
			}

			// P1
			_identifierOverlayEntryDict.TryGetValue("OnlineP1", out IOverlayEntry p1Entry);

			if (p1Entry != null)
			{
				p1Entry.ValueUnitFormat = "FPS";
				p1Entry.ValueAlignmentAndDigits = "{0,4:F0}";
			}

			// P0.2
			_identifierOverlayEntryDict.TryGetValue("OnlineP0dot2", out IOverlayEntry p1dot2Entry);

			if (p1dot2Entry != null)
			{
				p1dot2Entry.ValueUnitFormat = "FPS";
				p1dot2Entry.ValueAlignmentAndDigits = "{0,4:F0}";
			}
		}

		// To do : Retrieve values from RTSS
		private void SetRTSSMetricIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
				(x.Identifier == "Framerate" || x.Identifier == "Frametime")))
			{
				entry.IsNumeric = true;
			}
		}

		private void SetHardwareIsNumericState()
		{
			foreach (var entry in _overlayEntries.Where(x =>
			   (x.OverlayEntryType == EOverlayEntryType.GPU
				|| x.OverlayEntryType == EOverlayEntryType.CPU
				|| x.OverlayEntryType == EOverlayEntryType.RAM)))
			{
				entry.IsNumeric = true;
			}
		}

		private void UpdateFormatting()
		{
			foreach (var entry in _overlayEntries)
			{
				if (entry.FormatChanged)
				{
					// group name format
					var basicGroupFormat = entry.GroupSeparators == 0 ? "{0}" 
						: Enumerable.Repeat("\n", entry.GroupSeparators).Aggregate((i, j) => i + j) + "{0}";
					var groupNameFormatStringBuilder = new StringBuilder();
					groupNameFormatStringBuilder.Append("<S=");
					groupNameFormatStringBuilder.Append(entry.GroupFontSize.ToString());
					groupNameFormatStringBuilder.Append("><C=");
					groupNameFormatStringBuilder.Append(entry.GroupColor);
					groupNameFormatStringBuilder.Append(">");
					groupNameFormatStringBuilder.Append(basicGroupFormat);
					groupNameFormatStringBuilder.Append("  <C><S>");
					entry.GroupNameFormat = groupNameFormatStringBuilder.ToString();
						// "<S=" + entry.GroupFontSize + "><C=" + entry.GroupColor + ">" + basicGroupFormat + "  <C><S>";

					// value format
					if (entry.Identifier == "Framerate")
					{
						var valueFormatStringBuilder = new StringBuilder();
						valueFormatStringBuilder.Append("<A=-4><S=");
						valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
						valueFormatStringBuilder.Append("><C=");
						valueFormatStringBuilder.Append(entry.Color);
						valueFormatStringBuilder.Append("><FR><C><S><A>");
						valueFormatStringBuilder.Append("<A=4><S=");
						valueFormatStringBuilder.Append((entry.ValueFontSize / 2).ToString());
						valueFormatStringBuilder.Append("><C=");
						valueFormatStringBuilder.Append(entry.Color);
						valueFormatStringBuilder.Append(">FPS<C><S><A>");
						entry.ValueFormat = valueFormatStringBuilder.ToString();
							//"<A=-4><S=" + entry.ValueFontSize.ToString() + "><C=" + entry.Color + "><FR><C><S><A>" +
							//"<A=4><S=" + (entry.ValueFontSize / 2).ToString() + "><C=" + entry.Color + ">FPS<C><S><A>";
					}
					else if (entry.Identifier == "Frametime")
					{
						var valueFormatStringBuilder = new StringBuilder();
						valueFormatStringBuilder.Append("<A=-4><S=");
						valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
						valueFormatStringBuilder.Append("><C=");
						valueFormatStringBuilder.Append(entry.Color);
						valueFormatStringBuilder.Append("><FT><C><S><A>");
						valueFormatStringBuilder.Append("<A=4><S=");
						valueFormatStringBuilder.Append((entry.ValueFontSize / 2).ToString());
						valueFormatStringBuilder.Append("><C=");
						valueFormatStringBuilder.Append(entry.Color);
						valueFormatStringBuilder.Append(">ms<C><S><A>");
						entry.ValueFormat = valueFormatStringBuilder.ToString();
							//"<A=-4><S=" + entry.ValueFontSize.ToString() + "><C=" + entry.Color + "><FT><C><S><A>" +
							//"<A=4><S=" + (entry.ValueFontSize / 2).ToString() + "><C=" + entry.Color + ">ms<C><S><A>";
					}
					else
					{
						if (entry.ValueUnitFormat != null && entry.ValueAlignmentAndDigits != null)
						{
							var valueFormatStringBuilder = new StringBuilder();
							valueFormatStringBuilder.Append("<S=");
							valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
							valueFormatStringBuilder.Append("><C=");
							valueFormatStringBuilder.Append(entry.Color);
							valueFormatStringBuilder.Append(">");
							valueFormatStringBuilder.Append(entry.ValueAlignmentAndDigits);
							valueFormatStringBuilder.Append("<C><S>");
							valueFormatStringBuilder.Append("<S=");
							valueFormatStringBuilder.Append((entry.ValueFontSize / 2).ToString());
							valueFormatStringBuilder.Append("><C=");
							valueFormatStringBuilder.Append(entry.Color);
							valueFormatStringBuilder.Append(">");
							valueFormatStringBuilder.Append(entry.ValueUnitFormat);
							valueFormatStringBuilder.Append("<C><S>");
							entry.ValueFormat = valueFormatStringBuilder.ToString();
								// "<S=" + entry.ValueFontSize + "><C=" + entry.Color + ">" + entry.ValueAlignmentAndDigits + "<C><S>"
								//	+ "<S=" + entry.ValueFontSize / 2 + "><C=" + entry.Color + ">" + entry.ValueUnitFormat + "<C><S>";
						}
						else
						{
							var valueFormatStringBuilder = new StringBuilder();
							valueFormatStringBuilder.Append("<S=");
							valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
							valueFormatStringBuilder.Append("><C=");
							valueFormatStringBuilder.Append(entry.Color);
							valueFormatStringBuilder.Append(">{0}<C><S>");
							entry.ValueFormat = valueFormatStringBuilder.ToString();
							// "<S=" + entry.ValueFontSize + "><C=" + entry.Color + ">{0}<C><S>";
						}
					}

					// reset format changed 
					entry.FormatChanged = false;
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

							if (!double.TryParse(entry.UpperLimitValue, out double convertedUpperValue))
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

								if (!double.TryParse(entry.LowerLimitValue, out double convertedLowerValue))
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
							// Update color and last limit state

							if (entry.ValueUnitFormat != null && entry.ValueAlignmentAndDigits != null)
							{
								var valueFormatStringBuilder = new StringBuilder();
								valueFormatStringBuilder.Append("<S=");
								valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
								valueFormatStringBuilder.Append("><C=");
								valueFormatStringBuilder.Append(currentColor);
								valueFormatStringBuilder.Append(">");
								valueFormatStringBuilder.Append(entry.ValueAlignmentAndDigits);
								valueFormatStringBuilder.Append("<C><S>");
								valueFormatStringBuilder.Append("<S=");
								valueFormatStringBuilder.Append((entry.ValueFontSize / 2).ToString());
								valueFormatStringBuilder.Append("><C=");
								valueFormatStringBuilder.Append(currentColor);
								valueFormatStringBuilder.Append(">");
								valueFormatStringBuilder.Append(entry.ValueUnitFormat);
								valueFormatStringBuilder.Append("<C><S>");
								entry.ValueFormat = valueFormatStringBuilder.ToString();
									// "<S=" + entry.ValueFontSize + "><C=" + currentColor + ">" + entry.ValueAlignmentAndDigits + "<C><S>"
									// + "<S=" + entry.ValueFontSize / 2 + "><C=" + currentColor + ">" + entry.ValueUnitFormat + "<C><S>";
							}
							else
							{
								var valueFormatStringBuilder = new StringBuilder();
								valueFormatStringBuilder.Append("<S=");
								valueFormatStringBuilder.Append(entry.ValueFontSize.ToString());
								valueFormatStringBuilder.Append("><C=");
								valueFormatStringBuilder.Append(currentColor);
								valueFormatStringBuilder.Append(">{0}<C><S>");
								entry.ValueFormat = valueFormatStringBuilder.ToString();
									// "<S=" + entry.ValueFontSize + "><C=" + currentColor + ">{0}<C><S>";
							}

							entry.LastLimitState = limitState;
						}
					}
				}
			}
		}
		private string GetConfigurationFileName()
		{
			return Path.Combine(OVERLAY_CONFIG_FOLDER, $"OverlayEntryConfiguration_" +
				$"{_appConfiguration.OverlayEntryConfigurationFile}.json");
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
	}
}
