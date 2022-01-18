using System.Reactive.Subjects;

namespace CapFrameX.Monitoring.Contracts
{
    public interface IProcessService
    {
        ISubject<int> ProcessIdStream { get; }
    }
}
