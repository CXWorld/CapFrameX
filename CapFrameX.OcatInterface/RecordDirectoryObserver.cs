using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Collections.Generic;
using CapFrameX.Contracts.OcatInterface;

namespace CapFrameX.OcatInterface
{
	public class RecordDirectoryObserver : IRecordDirectoryObserver
	{
		private readonly string _recordDirectory;
		private readonly FileSystemWatcher _fileSystemWatcher;
		private readonly ISubject<string> _recordCreatedStream;
		private readonly ISubject<string> _recordDeletedStream;

		public bool IsActive { get; set; }

		public IObservable<FileInfo> RecordCreatedStream
			=> _recordCreatedStream.Where(p => IsActive).Select(path => new FileInfo(path)).AsObservable();

		public IObservable<FileInfo> RecordDeletedStream
			=> _recordDeletedStream.Where(p => IsActive).Select(path => new FileInfo(path)).AsObservable();

		public RecordDirectoryObserver()
		{
			// ToDo: Get from config
			var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			_recordDirectory = Path.Combine(documentFolder, @"OCAT\Captures");

			if (!Directory.Exists(_recordDirectory))
			{
				Directory.CreateDirectory(_recordDirectory);
			}

			_fileSystemWatcher = new FileSystemWatcher(_recordDirectory);
			_fileSystemWatcher.Created += new FileSystemEventHandler(WatcherCreated);
			_fileSystemWatcher.Deleted += new FileSystemEventHandler(WatcherDeleted);
			_fileSystemWatcher.EnableRaisingEvents = true;
			_fileSystemWatcher.IncludeSubdirectories = false;

			IsActive = false;
			_recordCreatedStream = new Subject<string>();
			_recordDeletedStream = new Subject<string>();
		}

		private void WatcherCreated(object sender, FileSystemEventArgs e)
		{
			// ToDo: Remove test output
			Console.WriteLine("Created, NAME: " + e.Name);
			Console.WriteLine("Created, FULLPATH: " + e.FullPath);

			_recordCreatedStream.OnNext(e.FullPath);
		}

		private void WatcherDeleted(object sender, FileSystemEventArgs e)
		{
			// ToDo: Remove test output
			Console.WriteLine("Deleted, NAME: " + e.Name);
			Console.WriteLine("Deleted, FULLPATH: " + e.FullPath);

			_recordDeletedStream.OnNext(e.FullPath);
		}

		public IEnumerable<FileInfo> GetAllRecordFileInfo()
		{
			return Directory.GetFiles(_recordDirectory, "*.csv",
										 SearchOption.TopDirectoryOnly).Select(file => new FileInfo(file));
		}
	}
}
