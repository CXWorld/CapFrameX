using CapFrameX.Contracts.Overlay;
using CapFrameX.Extensions;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CapFrameX.ViewModel.SubModels
{
	public class GroupNameSeparatorEntry : BindableBase
	{
		private string _groupName;
		private int _groupSeparators;

		public string GroupName
		{
			get => _groupName;
			set
			{
				_groupName = value;
				RaisePropertyChanged();
			}
		}

		public int GroupSeparators
		{
			get => _groupSeparators;
			set
			{
				_groupSeparators = value >= 0 ? value : 0;
				RaisePropertyChanged();
				UpdateGroupSeparator?.Invoke(_groupName, _groupSeparators);
			}
		}

		public Action<string, int> UpdateGroupSeparator { get; set; }

		public Action PropertyChangedAction { set; get; }

		/// <summary>
		/// Set field without side effects
		/// </summary>
		/// <param name="count"></param>
		public void SetGroupSeparators(int count)
		{
			_groupSeparators = count;
			PropertyChanged += (s, e) => PropertyChangedAction?.Invoke();
		}
	}

	public class OverlayGroupSeparating : BindableBase
	{
		private readonly OverlayViewModel _overlayViewModel;

		private int _selectedOverlayGroupIndex;

		public ObservableCollection<GroupNameSeparatorEntry> OverlayGroupNameSeparatorEntries { get; private set; }
			= new ObservableCollection<GroupNameSeparatorEntry>();

		public int SelectedOverlayGroupIndex
		{
			get => _selectedOverlayGroupIndex;
			set
			{
				_selectedOverlayGroupIndex = value;
				RaisePropertyChanged();
			}
		}

		public OverlayGroupSeparating(OverlayViewModel overlayViewModel)
		{
			_overlayViewModel = overlayViewModel;
		}

		public void SetOverlayEntries(IEnumerable<IOverlayEntry> overlayEntries)
			=> AddGroupNameSeparatorEntries(overlayEntries);

		public void UpdateGroupName(string groupName)
		{
			if (_overlayViewModel.OverlayEntries == null || !_overlayViewModel.OverlayEntries.Any())
				return;

			// update separators if group name is changed to existing separator entry
			if (!string.IsNullOrWhiteSpace(groupName))
			{
				var separatorEntry = OverlayGroupNameSeparatorEntries.FirstOrDefault(entry => entry.GroupName == groupName);

				if (separatorEntry != null)
				{
					int separator = separatorEntry.GroupSeparators;
					var updateSeparatorEntry = _overlayViewModel.OverlayEntries
						.FirstOrDefault(item => item.GroupName == groupName && item.GroupSeparators != separator);

					if (updateSeparatorEntry != null)
					{
						updateSeparatorEntry.GroupSeparators = separator;
						updateSeparatorEntry.FormatChanged = true;
					}
				}
			}

			AddGroupNameSeparatorEntries(_overlayViewModel.OverlayEntries);
			_overlayViewModel.UpdateGroupNameEnable();
		}

		private void AddGroupNameSeparatorEntries(IEnumerable<IOverlayEntry> overlayEntries)
		{
			OverlayGroupNameSeparatorEntries.Clear();
			OverlayGroupNameSeparatorEntries.AddRange
				(overlayEntries.Where(entry => !string.IsNullOrWhiteSpace(entry.GroupName))
				.Select(entry =>
				{
					var grpSepEntry = new GroupNameSeparatorEntry()
					{
						GroupName = entry.GroupName,
						UpdateGroupSeparator = UpdateGroupSeparator
					};
					grpSepEntry.SetGroupSeparators(entry.GroupSeparators);

					return grpSepEntry;
				}).DistinctBy(entry => entry.GroupName));
		}

		private void UpdateGroupSeparator(string groupName, int separators)
		{
			if (_overlayViewModel.OverlayEntries == null || !_overlayViewModel.OverlayEntries.Any())
				return;

			var targetEntries = _overlayViewModel.OverlayEntries.Where(entry => entry.GroupName == groupName);

			if (targetEntries != null && targetEntries.Any())
			{
				foreach (var targetEntry in targetEntries)
				{
					targetEntry.GroupSeparators = separators;
				}
			}
		}
	}
}