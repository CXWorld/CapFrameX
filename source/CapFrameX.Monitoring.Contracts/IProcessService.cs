using System.Reactive.Subjects;

namespace CapFrameX.Monitoring.Contracts
{
    public interface IProcessService
    {
        ISubject<int> ProcessIdStream { get; }

        /// <summary>
        /// Number of entries in the capture process list. Unlike <see cref="ProcessIdStream"/>,
        /// which stays 0 while several processes are detected and none is selected, this tells
        /// whether anything was detected at all.
        /// </summary>
        ISubject<int> ProcessCountStream { get; }
    }
}
