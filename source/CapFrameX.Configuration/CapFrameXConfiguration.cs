using CapFrameX.Contracts.Configuration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CapFrameX.Configuration
{
	public class CapFrameXConfiguration : IAppConfiguration
	{
		private readonly ISettingsStorage _settingsStorage;

		// General Settings

		public int MovingAverageWindowSize
		{
			get => Get<int>(100);
			set => Set(value);
		}

		public int IntervalAverageWindowTime
		{
			get => Get<int>(500);
			set => Set(value);
		}

		public string ScreenshotDirectory
		{
			get => Get<string>(@"MyDocuments\CapFrameX\Screenshots");
			set => Set(value);
		}

		public int FpsValuesRoundingDigits
		{
			get => Get<int>(1);
			set => Set(value);
		}

		public string HardwareInfoSource
		{
			get => Get<string>("Auto");
			set => Set(value);
		}

		public string CustomCpuDescription
		{
			get => Get<string>("Cpu");
			set => Set(value);
		}

		public string CustomGpuDescription
		{
			get => Get<string>("Gpu");
			set => Set(value);
		}

		public string CustomRamDescription
		{
			get => Get<string>("Ram");
			set => Set(value);
		}

		public bool StartMinimized
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool Autostart
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool IsGpuAccelerationActive
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public int HorizontalGraphExportRes
		{
			get => Get<int>(1200);
			set => Set(value);
		}

		public int VerticalGraphExportRes
		{
			get => Get<int>(600);
			set => Set(value);
		}

		// Record List Settings

		public string ObservedDirectory
		{
			get => Get<string>(@"MyDocuments\CapFrameX\Captures");
			set => Set(value);
		}

		public string CaptureRootDirectory
		{
			get => Get<string>(@"MyDocuments\CapFrameX\Captures");
			set => Set(value);
		}

		public string RecordingListSortMemberPath
		{
			get => Get<string>("GameName");
			set => Set(value);
		}

		public string RecordingListSortDirection
		{
			get => Get<string>("Ascending");
			set => Set(value);
		}

		// Capture Settings

		public string CaptureHotKey
		{
			get => Get<string>("F11");
			set => Set(value);
		}

		public double CaptureTime
		{
			get => Get<double>(30d);
			set => Set(value);
		}

		public string HotkeySoundMode
		{
			get => Get<string>("Voice");
			set => Set(value);
		}

		public double VoiceSoundLevel
		{
			get => Get<double>(0.25);
			set => Set(value);
		}

		public double SimpleSoundLevel
		{
			get => Get<double>(0.25);
			set => Set(value);
		}

		public string CaptureFileMode
		{
			get => Get<string>("Json");
			set => Set(value);
		}

		// Analysis Settings

		public double StutteringFactor
		{
			get => Get<double>(2.5);
			set => Set(value);
		}

		public double StutteringThreshold
		{
			get => Get<double>(25d);
			set => Set(value);
		}

		public bool UseSingleRecordMaxStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecord99QuantileStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public string HotkeySoundMode
        {
            get => Get<string>("Voice");
            set => Set(value);
        }

		public bool UseSingleRecordAverageStatisticParameter
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseSingleRecordMedianStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecordP5QuantileStatisticParameter
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseSingleRecordP1QuantileStatisticParameter
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseSingleRecordP0Dot1QuantileStatisticParameter
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseSingleRecordP0Dot2QuantileStatisticParameter
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseSingleRecordP1LowAverageStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecordP0Dot1LowAverageStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecordMinStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecordAdaptiveSTDStatisticParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecordCpuFpsPerWattParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseSingleRecordGpuFpsPerWattParameter
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool ShowOutlierWarning
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool ShowThresholdTimes
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool AreThresholdsReversed
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool AreThresholdsPercentage
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		// Aggregation Settings

		public string SecondMetricAggregation
		{
			get => Get<string>("P1");
			set => Set(value);
		}

		public string ThirdMetricAggregation
		{
			get => Get<string>("P0dot2");
			set => Set(value);
		}

		public string RelatedMetricAggregation
		{
			get => Get<string>("Second");
			set => Set(value);
		}

		public int OutlierPercentageAggregation
		{
			get => Get<int>(3);
			set => Set(value);
		}

		// Overlay Settings

		public int OverlayEntryConfigurationFile
		{
			get => Get<int>(0);
			set => Set(value);
		}

		public string OverlayHotKey
		{
			get => Get<string>("Alt+O");
			set => Set(value);
		}

		public string OverlayConfigHotKey
		{
			get => Get<string>("Alt+C");
			set => Set(value);
		}

		public int OSDRefreshPeriod
		{
			get => Get<int>(500);
			set => Set(value);
		}

		public bool AutoDisableOverlay
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool ToggleGlobalRTSSOSD
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool UseRunHistory
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public string ResetHistoryHotkey
		{
			get => Get<string>("Alt+R");
			set => Set(value);
		}

		public string RunHistorySecondMetric
		{
			get => Get<string>("P1");
			set => Set(value);
		}

		public string RunHistoryThirdMetric
		{
			get => Get<string>("P0dot2");
			set => Set(value);
		}

		public int SelectedHistoryRuns
		{
			get => Get<int>(3);
			set => Set(value);
		}

		public bool IsOverlayActive
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public bool UseAggregation
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool SaveAggregationOnly
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public string OutlierHandling
		{
			get => Get<string>("Replace");
			set => Set(value);
		}

		public string RelatedMetricOverlay
		{
			get => Get<string>("Second");
			set => Set(value);
		}

		public int OutlierPercentageOverlay
		{
			get => Get<int>(3);
			set => Set(value);
		}



		// Comparison Settings

		public string ComparisonFirstMetric
		{
			get => Get<string>("Average");
			set => Set(value);
		}

		public string ComparisonSecondMetric
		{
			get => Get<string>("P1");
			set => Set(value);
		}

		public string ComparisonThirdMetric
		{
			get => Get<string>("P0dot2");
			set => Set(value);
		}
		public string ComparisonContext
		{
			get => Get<string>("CPU");
			set => Set(value);
		}
		public string SecondComparisonContext
		{
			get => Get<string>("GPU");
			set => Set(value);
		}

		// Sensor Settings

		public bool UseSensorLogging
		{
			get => Get<bool>(true);
			set => Set(value);
		}

		public int SensorLoggingRefreshPeriod
		{
			get => Get<int>(250);
			set => Set(value);
		}


		// Report Settings


		// Sync Settings

		public int InputLagOffset
		{
			get => Get<int>(10);
			set => Set(value);
		}

		public string SyncRangeLower
		{
			get => Get<string>(40.ToString());
			set => Set(value);
		}

		public string SyncRangeUpper
		{
			get => Get<string>(120.ToString());
			set => Set(value);
		}

		// Cloud Settings

		public string CloudDownloadDirectory
		{
			get => Get<string>(@"MyDocuments\CapFrameX\Captures\Cloud");
			set => Set(value);
		}

		public bool ShareProcessListEntries
		{
			get => Get<bool>(false);
			set => Set(value);
		}

		public bool AutoUpdateProcessList
		{
			get => Get<bool>(false);
			set => Set(value);
		}


		

		T Get<T>(T defaultValue, [CallerMemberName] string key = null)
		{
			if(string.IsNullOrWhiteSpace(key))
            {
				throw new ArgumentException(key);
            }

            try
            {
				return _settingsStorage.GetValue<T>(key);
			} catch (KeyNotFoundException)
            {
				Set(defaultValue, key);
				return defaultValue;
            }
		}

		void Set<T>(T value, [CallerMemberName] string key = null)
        {
			if (string.IsNullOrWhiteSpace(key))
			{
				throw new ArgumentException(key);
			}
			_settingsStorage.SetValue(key, value);
		}

		public CapFrameXConfiguration(ISettingsStorage settingsStorage)
        {
            _settingsStorage = settingsStorage;
			_settingsStorage.Load().Wait();
        }
	}
}
