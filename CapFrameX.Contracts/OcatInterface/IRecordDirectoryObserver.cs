using System;
using System.IO;
using System.Collections.Generic;

namespace CapFrameX.Contracts.OcatInterface
{
    public interface IRecordDirectoryObserver
    {
        bool IsActive { get; set; }

        IObservable<string> RecordCreatedStream { get; }

        IObservable<string> RecordDeletedStream { get; }

        IEnumerable<FileInfo> GetAllRecordFileInfo();
    }
}
