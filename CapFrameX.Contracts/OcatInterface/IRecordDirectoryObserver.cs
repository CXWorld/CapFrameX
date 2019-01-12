using System;
using System.IO;
using System.Collections.Generic;

namespace CapFrameX.Contracts.OcatInterface
{
    public interface IRecordDirectoryObserver
    {
        bool IsActive { get; set; }

		IObservable<FileInfo> RecordCreatedStream { get; }

        IObservable<FileInfo> RecordDeletedStream { get; }

		void UpdateObservedDirectory(string directory);

		IEnumerable<FileInfo> GetAllRecordFileInfo();
    }
}
