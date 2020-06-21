using CapFrameX.Contracts.Overlay;
using CapFrameX.Extensions;
using Prism.Mvvm;
using System;
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
                _groupSeparators = value;
                RaisePropertyChanged();
                UpdateGroupSeparator?.Invoke(_groupName, _groupSeparators);
            }
        }

        public Action<string, int> UpdateGroupSeparator { get; set; }

        /// <summary>
        /// Set field without side effects
        /// </summary>
        /// <param name="count"></param>
        public void SetGroupSeparators(int count)
        {
            _groupSeparators = count;
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
            get { return _selectedOverlayGroupIndex; }
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

        public void SetOverlayEntries(IOverlayEntry[] overlayEntries)
        {
            OverlayGroupNameSeparatorEntries.Clear();
            OverlayGroupNameSeparatorEntries.AddRange
                (overlayEntries.Where(entry => !string.IsNullOrWhiteSpace(entry.GroupName))
                .Select(entry =>
                {
                    var grpSepEntry = new GroupNameSeparatorEntry()
                    {
                        GroupName = entry.GroupName
                    };
                    grpSepEntry.SetGroupSeparators(entry.GroupSeparators);

                    return grpSepEntry;
                }).DistinctBy(entry => entry.GroupName));
        }

        public void UpdateGroupName(string oldName, string newName)
        {
            var targetEntry = OverlayGroupNameSeparatorEntries
                .FirstOrDefault(entry => entry.GroupName == oldName);

            if (targetEntry != null)
            {
                targetEntry.GroupName = newName;

                var dublicatedEntries = OverlayGroupNameSeparatorEntries.Where(entry => entry.GroupName == newName);
                if (dublicatedEntries.Count() > 1)
                {
                    foreach (var item in dublicatedEntries.Skip(1))
                    {
                        OverlayGroupNameSeparatorEntries.Remove(item);
                    }
                }
            }
        }
    }
}