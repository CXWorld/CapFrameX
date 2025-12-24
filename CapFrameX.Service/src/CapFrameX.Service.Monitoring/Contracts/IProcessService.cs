using System.Reactive.Subjects;

namespace CapFrameX.Service.Monitoring.Contracts;

public interface IProcessService
{
    ISubject<int> ProcessIdStream { get; }
}
