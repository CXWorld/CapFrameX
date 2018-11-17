using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.OcatInterface
{
    public class RecordDirectoryObserver
    {
        private readonly string _recordDirectory;
        private readonly FileSystemWatcher _fileSystemWatcher;
        private readonly ISubject<string> _recordCreatedStream;
        private readonly ISubject<string> _recordDeletedStream;

        public bool IsActive { get; set; }

        public IObservable<string> RecordCreatedStream => _recordCreatedStream.Where(p => IsActive).AsObservable();

        public IObservable<string> RecordDeletedStream => _recordDeletedStream.Where(p => IsActive).AsObservable();

        public RecordDirectoryObserver()
        {
            // ToDo: Get from config
            _recordDirectory = @"C:\Users\marcf\OneDrive\Dokumente\OCAT\Recordings";
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
    }
}
