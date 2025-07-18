using System;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Capture.Contracts
{
    public interface IRecordDirectoryObserver
    {
        IObservable<FileInfo> FileCreatedStream { get; }
        IObservable<FileInfo> FileChangedStream { get; }
        IObservable<FileInfo> FileDeletedStream { get; }
        IObservable<IEnumerable<FileInfo>> DirectoryFilesStream { get; }
        IObservable<DirectoryInfo> ObservingDirectoryStream { get; }

        void ObserveDirectory(string dir);

        void RefreshCurrentDirectory();
    }
}
