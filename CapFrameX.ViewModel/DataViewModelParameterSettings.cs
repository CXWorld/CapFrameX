namespace CapFrameX.ViewModel
{
	public partial class DataViewModel
	{
		private bool _useMaxStatisticParameter;
		private bool _useP99QuantileStatisticParameter;
		private bool _useP95QuantileStatisticParameter;
		private bool _useAverageStatisticParameter;
		private bool _useP5QuantileStatisticParameter;
		private bool _useP1QuantileStatisticParameter;
		private bool _useP0Dot1QuantileStatisticParameter;
		private bool _useP1LowAverageStatisticParameter;
		private bool _useP0Dot1LowAverageStatisticParameter;
		private bool _useMinStatisticParameter;
		private bool _useAdaptiveSTDStatisticParameter;

		partial void InitializeStatisticParameter()
		{
			UseMaxStatisticParameter = _appConfiguration.UseSingleRecordMaxStatisticParameter;
			UseP99QuantileStatisticParameter = _appConfiguration.UseSingleRecord99QuantileStatisticParameter;
			UseP95QuantileStatisticParameter = _appConfiguration.UseSingleRecordP95QuantileStatisticParameter;
			UseAverageStatisticParameter = _appConfiguration.UseSingleRecordAverageStatisticParameter;
			UseP5QuantileStatisticParameter = _appConfiguration.UseSingleRecordP5QuantileStatisticParameter;
			UseP1QuantileStatisticParameter = _appConfiguration.UseSingleRecordP1QuantileStatisticParameter;
			UseP0Dot1QuantileStatisticParameter = _appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter;
			UseP1LowAverageStatisticParameter = _appConfiguration.UseSingleRecordP1LowAverageStatisticParameter;
			UseP0Dot1LowAverageStatisticParameter = _appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter;
			UseMinStatisticParameter = _appConfiguration.UseSingleRecordMinStatisticParameter;
			UseAdaptiveSTDStatisticParameter = _appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter;
		}

		public bool UseMaxStatisticParameter
		{
			get { return _useMaxStatisticParameter; }
			set
			{
				_useMaxStatisticParameter = value;
				_appConfiguration.UseSingleRecordMaxStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP99QuantileStatisticParameter
		{
			get { return _useP99QuantileStatisticParameter; }
			set
			{
				_useP99QuantileStatisticParameter = value;
				_appConfiguration.UseSingleRecord99QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP95QuantileStatisticParameter
		{
			get { return _useP95QuantileStatisticParameter; }
			set
			{
				_useP95QuantileStatisticParameter = value;
				_appConfiguration.UseSingleRecordP95QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseAverageStatisticParameter
		{
			get { return _useAverageStatisticParameter; }
			set
			{
				_useAverageStatisticParameter = value;
				_appConfiguration.UseSingleRecordAverageStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP5QuantileStatisticParameter
		{
			get { return _useP5QuantileStatisticParameter; }
			set
			{
				_useP5QuantileStatisticParameter = value;
				_appConfiguration.UseSingleRecordP5QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP1QuantileStatisticParameter
		{
			get { return _useP1QuantileStatisticParameter; }
			set
			{
				_useP1QuantileStatisticParameter = value;
				_appConfiguration.UseSingleRecordP1QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP0Dot1QuantileStatisticParameter
		{
			get { return _useP0Dot1QuantileStatisticParameter; }
			set
			{
				_useP0Dot1QuantileStatisticParameter = value;
				_appConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP1LowAverageStatisticParameter
		{
			get { return _useP1LowAverageStatisticParameter; }
			set
			{
				_useP1LowAverageStatisticParameter = value;
				_appConfiguration.UseSingleRecordP1LowAverageStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP0Dot1LowAverageStatisticParameter
		{
			get { return _useP0Dot1LowAverageStatisticParameter; }
			set
			{
				_useP0Dot1LowAverageStatisticParameter = value;
				_appConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseMinStatisticParameter
		{
			get { return _useMinStatisticParameter; }
			set
			{
				_useMinStatisticParameter = value;
				_appConfiguration.UseSingleRecordMinStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseAdaptiveSTDStatisticParameter
		{
			get { return _useAdaptiveSTDStatisticParameter; }
			set
			{
				_useAdaptiveSTDStatisticParameter = value;
				_appConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter = value;
				RaisePropertyChanged();
			}
		}
	}
}
