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
		private readonly TimeSpan _fileAccessIntervalTimespan = TimeSpan.FromMilliseconds(100);
		private readonly int _fileAccessIntervalRetryLimit = 10;

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

			HasValidSourceStream = new Subject<bool>();

			try
			{
				_recordDirectory = GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);
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

		private void WatcherCreated(object sender, FileSystemEventArgs e)
		{
			var fileInfo = new FileInfo(e.FullPath);
			Observable.Interval(_fileAccessIntervalTimespan)
				.Select(_ =>
				{
					using (var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.None)) {
						return fileInfo.FullName;
					}
				})
				.Retry(_fileAccessIntervalRetryLimit)
				.Take(1)
				.Subscribe(fullPath =>
				{
					_recordCreatedStream.OnNext(e.FullPath);
				}, error =>
				{
					_logger.LogError(error, "Error while watcher created handling");
				});

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
