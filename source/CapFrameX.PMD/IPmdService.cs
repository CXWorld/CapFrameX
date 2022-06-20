using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.PMD
{
    public interface IPmdService
    {
        IObservable<PmdChannel[]> PmdChannelStream { get; }
    }
}
