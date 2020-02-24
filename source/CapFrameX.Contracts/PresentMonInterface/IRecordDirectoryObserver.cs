using System;
using System.IO;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.PresentMonInterface
{
    public interface IRecordDirectoryObserver
    {
        IObservable<FileInfo> FileCreatedStream { get; }
        IObservable<FileInfo> FileChangedStream { get; }
        IObservable<FileInfo> FileDeletedStream { get; }
        IObservable<IEnumerable<FileInfo>> DirectoryFilesStream { get; }
        IObservable<DirectoryInfo> ObservingDirectoryStream { get; }

        void ObserveDirectory(string dir);
    }
}
