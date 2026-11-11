using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CapFrameX.CaptureHelper
{
    /// <summary>A row of the process list.</summary>
    public sealed class ProcessItem : INotifyPropertyChanged
    {
        private double _fps;

        public ProcessItem(int processId, string name)
        {
            ProcessId = processId;
            Name = name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int ProcessId { get; }

        public string Name { get; }

        public double Fps
        {
            get => _fps;
            set
            {
                if (_fps.Equals(value)) return;
                _fps = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Fps)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FpsText)));
            }
        }

        public string FpsText => _fps.ToString("0", CultureInfo.CurrentCulture);

        // Screen readers announce a row by this text.
        public override string ToString() => Name;
    }

    /// <summary>A folder or CSV file of the analysis tree. Folders read their content when expanded.</summary>
    public sealed class FileNode : INotifyPropertyChanged
    {
        private static readonly FileNode Placeholder = new FileNode();
        private bool _isExpanded;

        public FileNode(string path, bool isDirectory)
        {
            FullPath = path;
            IsDirectory = isDirectory;
            Name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(Name)) Name = path;
            if (isDirectory) Children.Add(Placeholder);
        }

        private FileNode()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get; }

        public string FullPath { get; }

        public bool IsDirectory { get; }

        public ObservableCollection<FileNode> Children { get; } = new ObservableCollection<FileNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                if (value) LoadChildren();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public override string ToString() => Name;

        private void LoadChildren()
        {
            if (Children.Count != 1 || Children[0] != Placeholder) return;
            Children.Clear();
            try
            {
                var directory = new DirectoryInfo(FullPath);
                if (!directory.Exists) return;
                foreach (var folder in directory.GetDirectories().OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
                    Children.Add(new FileNode(folder.FullName, true));
                foreach (var file in directory.GetFiles("*.csv").OrderByDescending(f => f.LastWriteTime))
                    Children.Add(new FileNode(file.FullName, false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Write($"Listing {FullPath} failed: {ex.Message}");
            }
        }
    }
}
