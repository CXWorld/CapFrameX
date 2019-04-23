using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.OcatInterface;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CapFrameX.ViewModel
{
	public class StateViewModel : BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IEventAggregator _eventAggregator;
		private readonly IAppConfiguration _appConfiguration;

		private bool IsBeta => GetBetaState();

		private bool GetBetaState()
		{
			Assembly assembly = GetAssemblyByName("CapFrameX");
			var metaData = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute));

			if (metaData.FirstOrDefault(attribute => (attribute as AssemblyMetadataAttribute).Key == "IsBeta") 
				is AssemblyMetadataAttribute isBetaAttribute)
				return Convert.ToBoolean(isBetaAttribute.Value);

			return true;
		}

		public bool IsDirectoryObserving
		{
			get { return _recordObserver.IsActive; }
			set
			{
				_recordObserver.IsActive =
					value && _recordObserver.HasValidSource;
				RaisePropertyChanged();
			}
		}

		public string VersionString
		{
			get
			{
				Assembly assembly = GetAssemblyByName("CapFrameX");
				var fileVersion = IsBeta ? string.Format(FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion + "{0}", " Beta") :
					FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
				return fileVersion;
			}
		}

		public StateViewModel(IRecordDirectoryObserver recordObserver,
							  IEventAggregator eventAggregator,
							  IAppConfiguration appConfiguration)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
			_appConfiguration = appConfiguration;

			_recordObserver.HasValidSourceStream
				.Subscribe(state => IsDirectoryObserving = state);
		}

		private Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}
	}
}
