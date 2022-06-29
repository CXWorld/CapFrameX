using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.PMD
{
    public struct PmdSample<T> where T : IComparable<T>
    {
        public long PerformanceCounter;
        public T Value; 
    }
}
