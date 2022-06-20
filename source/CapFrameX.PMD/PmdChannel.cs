using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.PMD
{
    public struct PmdChannel
    {
        public string Name;
        public PmdChannelType pmdChannelType;
        public float Current;
        public float Voltage;
        public float Power;
        public long TimeStamp;
    }
}
