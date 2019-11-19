using System;
using System.IO;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.OcatInterface
{
    public interface IRecordDirectoryObserver
    {
        bool IsActive { get; set; }

		bool HasValidSource { get; }

		FileSystemWatcher RecordingFileWatcher { get; }

		Subject<bool> HasValidSourceStream { get; }

		IObservable<FileInfo> RecordCreatedStream { get; }

        IObservable<FileInfo> RecordDeletedStream { get; }

		void UpdateObservedDirectory(string directory);

		IEnumerable<FileInfo> GetAllRecordFileInfo();
    }
}
