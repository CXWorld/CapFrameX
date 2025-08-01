using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using CapFrameX.Capture.Contracts;

namespace CapFrameX.Data
{
    public class RecordDirectoryObserver : IRecordDirectoryObserver, IDisposable
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<RecordDirectoryObserver> _logger;
        private readonly Func<FileInfo, bool> _validFileFilterFunc = (FileInfo fi) => fi.Extension == ".csv" || fi.Extension == ".json";
        private readonly ISubject<FileInfo> _fileCreatedSubject = new ReplaySubject<FileInfo>();
        private readonly ISubject<FileInfo> _fileChangedSubject = new ReplaySubject<FileInfo>();
        private readonly ISubject<FileInfo> _fileDeletedSubject = new ReplaySubject<FileInfo>();
        private readonly ISubject<IEnumerable<FileInfo>> _directoryFilesSubject = new BehaviorSubject<IEnumerable<FileInfo>>(new FileInfo[] { });
        private readonly ISubject<DirectoryInfo> _observingDirectorySubject = new BehaviorSubject<DirectoryInfo>(null);   

        public IObservable<FileInfo> FileCreatedStream => _fileCreatedSubject.AsObservable();
        public IObservable<FileInfo> FileChangedStream => _fileChangedSubject.AsObservable();
        public IObservable<FileInfo> FileDeletedStream => _fileDeletedSubject.AsObservable();
        public IObservable<IEnumerable<FileInfo>> DirectoryFilesStream => _directoryFilesSubject.AsObservable();
        public IObservable<DirectoryInfo> ObservingDirectoryStream => _observingDirectorySubject.AsObservable();

        [DllImport("shlwapi.dll")]
        public static extern bool PathIsNetworkPath(string pszPath);

        private FileSystemWatcher _watcher;
        private List<FileInfo> _currentFiles = new List<FileInfo>();
        private string _currentDir;

        public RecordDirectoryObserver(IAppConfiguration appConfiguration, ILogger<RecordDirectoryObserver> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;
            ObserveDirectory(GetInitialObservedDirectory(_appConfiguration.ObservedDirectory));
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _directoryFilesSubject.OnCompleted();
            _fileCreatedSubject.OnCompleted();
            _fileChangedSubject.OnCompleted();
            _fileDeletedSubject.OnCompleted();
            _observingDirectorySubject.OnCompleted();
        }

        public void RefreshCurrentDirectory()
        {
            if (string.IsNullOrWhiteSpace(_currentDir))
                return;

            _directoryFilesSubject.OnNext(_currentFiles);
        }

        public void ObserveDirectory(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir) || dir == _currentDir)
                return;

            _currentDir = dir;
            _appConfiguration.ObservedDirectory = dir;
            var directory = new DirectoryInfo(dir);
            if (!directory.Exists)
            {
                _logger.LogWarning("Cannot observe directory {path}: Does not exist", directory);
                _observingDirectorySubject.OnNext(directory);
                return;
            }

            if (_watcher is FileSystemWatcher)
            {
                _logger.LogDebug("Changing observed directory");
                _watcher.Dispose();
            }

            _watcher = new FileSystemWatcher(directory.FullName)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _observingDirectorySubject.OnNext(directory);
            _currentFiles = directory.GetFiles("*.*", SearchOption.TopDirectoryOnly).Where(_validFileFilterFunc).ToList();
            _directoryFilesSubject.OnNext(_currentFiles);
            _logger.LogInformation("Now observing directory: {path}", directory.FullName);

            void OnFileCreated(object sender, FileSystemEventArgs e)
            {
                if (!CheckRelevantFiles(e.FullPath))
                    return;

                var fileInfo = new FileInfo(e.FullPath);
                _logger.LogInformation("File created: {path}", fileInfo.FullName);
                _fileCreatedSubject.OnNext(fileInfo);
                _currentFiles.Add(fileInfo);
            }            

            void OnFileChanged(object sender, FileSystemEventArgs e)
            {
                if (_currentFiles == null || !_currentFiles.Any() || !CheckRelevantFiles(e.FullPath))
                    return;

                var fileInfo = _currentFiles.FirstOrDefault(f => f.FullName.Equals(e.FullPath));
                if (fileInfo is FileInfo)
                {
                    _logger.LogInformation("File changed: {path}", fileInfo.FullName);
                    fileInfo.Refresh();
                    _fileChangedSubject.OnNext(fileInfo);
                }
            }

            void OnFileRenamed(object sender, RenamedEventArgs e)
            {
                if (_currentFiles == null || !_currentFiles.Any() || !CheckRelevantFiles(e.OldFullPath))
                    return;

                var oldFileInfo = new FileInfo(e.OldFullPath);
                _fileDeletedSubject.OnNext(oldFileInfo);
                var item = _currentFiles.First(f => f.FullName.Equals(oldFileInfo.FullName));
                if (item is FileInfo)
                {
                    _currentFiles.Remove(item);
                }
                OnFileCreated(sender, e);
            }

            void OnFileDeleted(object sender, FileSystemEventArgs e)
            {
                if (_currentFiles == null || !_currentFiles.Any() || !CheckRelevantFiles(e.FullPath))
                    return;
                try
                {
                    var fileInfo = new FileInfo(e.FullPath);
                    _fileDeletedSubject.OnNext(fileInfo);
                    _currentFiles.Remove(_currentFiles.First(f => f.FullName.Equals(fileInfo.FullName)));
                    _logger.LogInformation("File deleted: {path}", fileInfo.FullName);
                }
                catch 
                {
                    var fileInfo = new FileInfo(e.FullPath);
                    _logger.LogInformation($"Error deleting file: {fileInfo.FullName}");
                }
            }
        }

        private bool CheckRelevantFiles(string path) 
            => path.EndsWith(".json") || path.EndsWith(".csv");

        private string GetInitialObservedDirectory(string observedDirectory)
        {
            var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = observedDirectory;

            // >= V1.3
            if (observedDirectory.Contains(@"MyDocuments\OCAT\Captures"))
            {
                path = Path.Combine(documentFolder, @"OCAT\Captures");
            }

            // < V1.3
            else if (observedDirectory.Contains(@"MyDocuments\OCAT\Recordings"))
            {
                path = Path.Combine(documentFolder, @"OCAT\Recordings");
            }

            // CX captures
            else if (observedDirectory.Contains(@"MyDocuments\CapFrameX\Captures"))
            {
                path = Path.Combine(documentFolder, @"CapFrameX\Captures");
            }

            return path;
        }
    }
}
