﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Data.Session.Contracts
{
    public interface ISessionSensorEntry
    {
        string Name { get; }
        string Type { get; }
        List<double> Values { get; }
    }
}
