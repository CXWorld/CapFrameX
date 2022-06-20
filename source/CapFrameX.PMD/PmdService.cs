using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.PMD
{
    public class PmdService : IPmdService
    {
        public IObservable<PmdChannel[]> PmdChannelStream { get; }

        public PmdService()
        {
        }
    }
}
