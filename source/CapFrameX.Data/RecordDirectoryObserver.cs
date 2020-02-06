using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.PresentMonInterface;
using Microsoft.Extensions.Logging;

namespace CapFrameX.Data
{
    public class RecordDirectoryObserver : IRecordDirectoryObserver
    {
        /// <summary>
        /// timeout in milliseconds
        /// </summary>
        private const int ACCESSTIMEOUT = 5000;

        private readonly ISubject<string> _recordCreatedStream;
        private readonly ISubject<string> _recordDeletedStream;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<RecordDirectoryObserver> _logger;
        private bool _isActive;
        private string _recordDirectory;
        private FileSystemWatcher _fileSystemWatcher;

        public bool IsActive
        {
            get { return _isActive; }
            set { _isActive = value; HasValidSourceStream.OnNext(_isActive); }
        }

        public bool HasValidSource { get; private set; }

        public IObservable<FileInfo> RecordCreatedStream
            => _recordCreatedStream.Where(p => IsActive).Select(path => new FileInfo(path)).AsObservable();

        public IObservable<FileInfo> RecordDeletedStream
            => _recordDeletedStream.Where(p => IsActive).Select(path => new FileInfo(path)).AsObservable();

        public FileSystemWatcher RecordingFileWatcher => _fileSystemWatcher;

        public Subject<bool> HasValidSourceStream { get; }

        public RecordDirectoryObserver(IAppConfiguration appConfiguration, ILogger<RecordDirectoryObserver> logger)
        {
            _appConfiguration = appConfiguration;
            _logger = logger;
            _recordDirectory = GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);

            HasValidSourceStream = new Subject<bool>();

            try
            {
                if (!Directory.Exists(_recordDirectory))
                {
                    Directory.CreateDirectory(_recordDirectory);
                }

                _fileSystemWatcher = new FileSystemWatcher(_recordDirectory);
                _fileSystemWatcher.Created += new FileSystemEventHandler(WatcherCreated);
                _fileSystemWatcher.Deleted += new FileSystemEventHandler(WatcherDeleted);
                _fileSystemWatcher.EnableRaisingEvents = true;
                _fileSystemWatcher.IncludeSubdirectories = false;

                HasValidSource = true;
                _logger.LogDebug("{componentName} Ready", this.GetType().Name);
            }
            catch (Exception ex)
            {
                HasValidSource = false;
                _logger.LogError(ex, "Error while creating record directory");
            }

            IsActive = false;
            _recordCreatedStream = new Subject<string>();
            _recordDeletedStream = new Subject<string>();
        }

        public static string GetInitialObservedDirectory(string observedDirectory)
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

        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        private void WatcherCreated(object sender, FileSystemEventArgs e)
        {
            var fileInfo = new FileInfo(e.FullPath);

            try
            {
                var task = Task.Run(() =>
                {
                    while (IsFileLocked(fileInfo));
                });

                if (task.Wait(TimeSpan.FromMilliseconds(ACCESSTIMEOUT)))
                {
                    _recordCreatedStream.OnNext(e.FullPath);
                    _logger.LogInformation("Watcher created event correctly handled");
                }
                else
                    throw new TimeoutException("File access timeout");
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, $"{e.FullPath} access timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while watcher created handling");
            }
        }

        private void WatcherDeleted(object sender, FileSystemEventArgs e)
        {
            _recordDeletedStream.OnNext(e.FullPath);
        }

        public IEnumerable<FileInfo> GetAllRecordFileInfo()
        {
            return HasValidSource ? Directory.GetFiles(_recordDirectory, "*.csv",
                SearchOption.TopDirectoryOnly)
                .Select(file => new FileInfo(file))
                : Enumerable.Empty<FileInfo>();
        }

        public void UpdateObservedDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                HasValidSource = false;
                IsActive = false;
            }
            else
            {
                HasValidSource = true;

                _recordDirectory = directory;
                _fileSystemWatcher = new FileSystemWatcher(directory);
                _fileSystemWatcher.Created += new FileSystemEventHandler(WatcherCreated);
                _fileSystemWatcher.Deleted += new FileSystemEventHandler(WatcherDeleted);
                _fileSystemWatcher.EnableRaisingEvents = true;
                _fileSystemWatcher.IncludeSubdirectories = false;

                IsActive = true;
            }

            HasValidSourceStream.OnNext(HasValidSource);
        }
    }
}
