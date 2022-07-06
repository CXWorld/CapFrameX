using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.PMD
{
    public interface IPmdDriver
    {
        IObservable<PmdChannel[]> PmdChannelStream { get; }

        EPmdDriverStatus GetPmdDriverStatus();

        bool Connect();

        bool Disconnect();
    }
}
