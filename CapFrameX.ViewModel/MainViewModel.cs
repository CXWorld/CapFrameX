using CapFrameX.Contracts.OcatInterface;
using CapFrameX.OcatInterface;
using Prism.Mvvm;
using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Threading;
using System.Reactive.Linq;
using System.Linq;

namespace CapFrameX.ViewModel
{
	public class MainViewModel: BindableBase
	{
		private readonly IRecordDirectoryObserver _recordObserver;
		private OcatRecordInfo _selectedRecordInfo;

		public ObservableCollection<OcatRecordInfo> RecordInfoList { get; } 
			= new ObservableCollection<OcatRecordInfo>();

		public OcatRecordInfo SelectedRecordInfo
		{
			get { return _selectedRecordInfo; }
			set
			{
				_selectedRecordInfo = value;
				RaisePropertyChanged();
				OnSelectedRecordInfoChanged();
			}
		}

		public MainViewModel(IRecordDirectoryObserver recordObserver)
		{
			_recordObserver = recordObserver;
			// ToDo: check wether to do this async
			var initialRecordList = _recordObserver.GetAllRecordFileInfo();

			foreach (var fileInfo in initialRecordList)
			{
				AddToRecordInfoList(fileInfo);
			}

			var context = SynchronizationContext.Current;
			_recordObserver.RecordCreatedStream.ObserveOn(context).SubscribeOn(context).Subscribe(OnRecordCreated);
			_recordObserver.RecordDeletedStream.ObserveOn(context).SubscribeOn(context).Subscribe(OnRecordDeleted);

			// Turn streams now on
			_recordObserver.IsActive = true;
		}

		private void AddToRecordInfoList(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				RecordInfoList.Add(recordInfo);
			}
		}

		private void OnRecordCreated(FileInfo fileInfo) => AddToRecordInfoList(fileInfo);

		private void OnRecordDeleted(FileInfo fileInfo)
		{
			var recordInfo = OcatRecordInfo.Create(fileInfo);
			if (recordInfo != null)
			{
				var match = RecordInfoList.FirstOrDefault(info => info.FullPath== fileInfo.FullName);

				if (match != null)
				{
					RecordInfoList.Remove(match);
				}
			}			
		}

		private void OnSelectedRecordInfoChanged()
		{
			var session = RecordManager.LoadData(SelectedRecordInfo.FullPath);

			//ToDo: draw something
		}
	}
}
