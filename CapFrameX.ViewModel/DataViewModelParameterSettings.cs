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
			UseMaxStatisticParameter = AppConfiguration.UseSingleRecordMaxStatisticParameter;
			UseP99QuantileStatisticParameter = AppConfiguration.UseSingleRecord99QuantileStatisticParameter;
			UseP95QuantileStatisticParameter = AppConfiguration.UseSingleRecordP95QuantileStatisticParameter;
			UseAverageStatisticParameter = AppConfiguration.UseSingleRecordAverageStatisticParameter;
			UseP5QuantileStatisticParameter = AppConfiguration.UseSingleRecordP5QuantileStatisticParameter;
			UseP1QuantileStatisticParameter = AppConfiguration.UseSingleRecordP1QuantileStatisticParameter;
			UseP0Dot1QuantileStatisticParameter = AppConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter;
			UseP1LowAverageStatisticParameter = AppConfiguration.UseSingleRecordP1LowAverageStatisticParameter;
			UseP0Dot1LowAverageStatisticParameter = AppConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter;
			UseMinStatisticParameter = AppConfiguration.UseSingleRecordMinStatisticParameter;
			UseAdaptiveSTDStatisticParameter = AppConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter;
		}

		public bool UseMaxStatisticParameter
		{
			get { return _useMaxStatisticParameter; }
			set
			{
				_useMaxStatisticParameter = value;
				AppConfiguration.UseSingleRecordMaxStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP99QuantileStatisticParameter
		{
			get { return _useP99QuantileStatisticParameter; }
			set
			{
				_useP99QuantileStatisticParameter = value;
				AppConfiguration.UseSingleRecord99QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP95QuantileStatisticParameter
		{
			get { return _useP95QuantileStatisticParameter; }
			set
			{
				_useP95QuantileStatisticParameter = value;
				AppConfiguration.UseSingleRecordP95QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseAverageStatisticParameter
		{
			get { return _useAverageStatisticParameter; }
			set
			{
				_useAverageStatisticParameter = value;
				AppConfiguration.UseSingleRecordAverageStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP5QuantileStatisticParameter
		{
			get { return _useP5QuantileStatisticParameter; }
			set
			{
				_useP5QuantileStatisticParameter = value;
				AppConfiguration.UseSingleRecordP5QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP1QuantileStatisticParameter
		{
			get { return _useP1QuantileStatisticParameter; }
			set
			{
				_useP1QuantileStatisticParameter = value;
				AppConfiguration.UseSingleRecordP1QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP0Dot1QuantileStatisticParameter
		{
			get { return _useP0Dot1QuantileStatisticParameter; }
			set
			{
				_useP0Dot1QuantileStatisticParameter = value;
				AppConfiguration.UseSingleRecordP0Dot1QuantileStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP1LowAverageStatisticParameter
		{
			get { return _useP1LowAverageStatisticParameter; }
			set
			{
				_useP1LowAverageStatisticParameter = value;
				AppConfiguration.UseSingleRecordP1LowAverageStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseP0Dot1LowAverageStatisticParameter
		{
			get { return _useP0Dot1LowAverageStatisticParameter; }
			set
			{
				_useP0Dot1LowAverageStatisticParameter = value;
				AppConfiguration.UseSingleRecordP0Dot1LowAverageStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseMinStatisticParameter
		{
			get { return _useMinStatisticParameter; }
			set
			{
				_useMinStatisticParameter = value;
				AppConfiguration.UseSingleRecordMinStatisticParameter = value;
				RaisePropertyChanged();
			}
		}

		public bool UseAdaptiveSTDStatisticParameter
		{
			get { return _useAdaptiveSTDStatisticParameter; }
			set
			{
				_useAdaptiveSTDStatisticParameter = value;
				AppConfiguration.UseSingleRecordAdaptiveSTDStatisticParameter = value;
				RaisePropertyChanged();
			}
		}
	}
}
