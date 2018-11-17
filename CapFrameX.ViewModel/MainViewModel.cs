using CapFrameX.OcatInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.ViewModel
{
    public class MainViewModel
    {
        public MainViewModel()
        {
            RecordDirectoryObserver recordObserver = new RecordDirectoryObserver();
        }
    }
}
