using CapFrameX.Contracts.OcatInterface;

namespace CapFrameX.ViewModel
{
    public class MainViewModel
    {
        private readonly IRecordDirectoryObserver _recordObserver;

        public MainViewModel(IRecordDirectoryObserver recordObserver)
        {
            _recordObserver = recordObserver;
        }
    }
}
