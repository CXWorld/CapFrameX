using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using CapFrameX.Contracts.OcatInterface;
using CapFrameX.Contracts.Configuration;

namespace CapFrameX.OcatInterface
{
	public class RecordDirectoryObserver : IRecordDirectoryObserver
	{
		private readonly ISubject<string> _recordCreatedStream;
		private readonly ISubject<string> _recordDeletedStream;
		private readonly IAppConfiguration _appConfiguration;

		private string _recordDirectory;
		private FileSystemWatcher _fileSystemWatcher;

		public bool IsActive { get; set; }

		public bool HasValidSource { get; private set; }

		public IObservable<FileInfo> RecordCreatedStream
			=> _recordCreatedStream.Where(p => IsActive).Select(path => new FileInfo(path)).AsObservable();

		public IObservable<FileInfo> RecordDeletedStream
			=> _recordDeletedStream.Where(p => IsActive).Select(path => new FileInfo(path)).AsObservable();

		public Subject<bool> HasValidSourceStream { get; }

		public RecordDirectoryObserver(IAppConfiguration appConfiguration)
		{
			_appConfiguration = appConfiguration;
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
			}
			catch
			{
				HasValidSource = false;
			}

			IsActive = false;
			_recordCreatedStream = new Subject<string>();
			_recordDeletedStream = new Subject<string>();
		}

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
			else if (observedDirectory.Contains(@"MyDocuments\OCAT\CRecordings"))
			{
				path = Path.Combine(documentFolder, @"OCAT\Recordings");
			}

			return path;
		}

		private void WatcherCreated(object sender, FileSystemEventArgs e)
		{
			if (!e.FullPath.Contains("CapFrameX"))
				_recordCreatedStream.OnNext(e.FullPath);
		}

		private void WatcherDeleted(object sender, FileSystemEventArgs e)
			=> _recordDeletedStream.OnNext(e.FullPath);

		public IEnumerable<FileInfo> GetAllRecordFileInfo()
		{
			return HasValidSource ?  Directory.GetFiles(_recordDirectory, "*.csv",
														 SearchOption.TopDirectoryOnly).Where(file => 
														 !file.Contains("CapFrameX") &&
														 !file.Contains("SearchUI") &&
														 !file.Contains("ShellExperienceHost") &&
														 !file.Contains("steamwebhelper")
														 ).Select(file => new FileInfo(file)) : null;
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
