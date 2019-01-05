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

		public bool IsDirectoryObserving
		{
			get { return _recordObserver.IsActive; }
			set { _recordObserver.IsActive = value; RaisePropertyChanged(); }
		}

		public string VersionString
		{
			get
			{
				Assembly assembly = GetAssemblyByName("CapFrameX");
				return FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
			}
		}

		public StateViewModel(IRecordDirectoryObserver recordObserver, IEventAggregator eventAggregator)
		{
			_recordObserver = recordObserver;
			_eventAggregator = eventAggregator;
		}

		private Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}
	}
}
