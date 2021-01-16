using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Contracts
{
    public interface ISessionSensorEntry<T>
    {
        string Name { get; }
        string Type { get; }
        LinkedList<T> Values { get; }
    }
}
