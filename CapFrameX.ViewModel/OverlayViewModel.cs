﻿using CapFrameX.EventAggregation.Messages;
using CapFrameX.MVVM.Dialogs;
using CapFrameX.OcatInterface;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
	public class OverlayViewModel : BindableBase, INavigationAware
	{
		private readonly IEventAggregator _eventAggregator;

		private PubSubEvent<ViewMessages.HideOverlay> _hideOverlayEvent;
		private bool _isEditingDialogOpen;
		private EditingDialog _editingDialogContent;
		private bool _useEventMessages;
		private Session _session;
		private OcatRecordInfo _recordInfo;
		private string _customCpuDescription;
		private string _customGpuDescription;
		private string _customComment;

		public bool IsEditingDialogOpen
		{
			get { return _isEditingDialogOpen; }
			set
			{
				_isEditingDialogOpen = value;
				RaisePropertyChanged();
			}
		}

		public EditingDialog EditingDialogContent
		{
			get { return _editingDialogContent; }
			set
			{
				_editingDialogContent = value;
				RaisePropertyChanged();
			}
		}

		public string CustomCpuDescription
		{
			get { return _customCpuDescription; }
			set
			{
				_customCpuDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomGpuDescription
		{
			get { return _customGpuDescription; }
			set
			{
				_customGpuDescription = value;
				RaisePropertyChanged();
			}
		}

		public string CustomComment
		{
			get { return _customComment; }
			set
			{
				_customComment = value;
				RaisePropertyChanged();
			}
		}

		public ICommand AcceptEditingDialogCommand { get; }
		public ICommand CancelEditingDialogCommand { get; }

		public OverlayViewModel(IEventAggregator eventAggregator)
		{
			_eventAggregator = eventAggregator;

			AcceptEditingDialogCommand = new DelegateCommand(OnAcceptEditingDialog);
			CancelEditingDialogCommand = new DelegateCommand(OnCancelEditingDialog);

			_hideOverlayEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.HideOverlay>>();
			SubscribeToUpdateSession();
		}

		private void OnCancelEditingDialog()
		{
			IsEditingDialogOpen = false;
			_hideOverlayEvent.Publish(new ViewMessages.HideOverlay());
		}

		private void OnAcceptEditingDialog()
		{
			IsEditingDialogOpen = false;
			var adjustedCustomCpuDescription = CustomCpuDescription.Replace(",", "").Replace(";", "");
			var adjustedCustomGpuDescription = CustomGpuDescription.Replace(",", "").Replace(";", "");
			var adjustedCustomComment = CustomComment.Replace(",", "").Replace(";", "");
			RecordManager.UpdateCustomData(_recordInfo, 
				adjustedCustomCpuDescription, adjustedCustomGpuDescription, adjustedCustomComment);
			_hideOverlayEvent.Publish(new ViewMessages.HideOverlay());
		}

		private void SubscribeToUpdateSession()
		{
			_eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
							.Subscribe(msg =>
							{
								_session = msg.OcatSession;
								_recordInfo = msg.RecordInfo;
							});
		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{
			_useEventMessages = true;

			if (_session != null)
			{
				CustomCpuDescription = string.Copy(_session.ProcessorName ?? "-");
				CustomGpuDescription = string.Copy(_session.GraphicCardName ?? "-");
				CustomComment = string.Copy(_session.Comment ?? "-");
			}
			else
			{
				CustomCpuDescription = "-";
				CustomGpuDescription = "-";
				CustomComment = "-";
			}

			EditingDialogContent = new EditingDialog();
			IsEditingDialogOpen = true;
		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{
			_useEventMessages = false;
		}
	}
}
