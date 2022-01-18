using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Sensor
{
    public interface IProcessService
    {
        ISubject<int> ProcessIdStream { get; }
    }
}
