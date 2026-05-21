// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Reactive.Linq;
using CapFrameX.Monitoring.Contracts;

namespace LibreHardwareMonitor.Hardware.Memory;

internal sealed class TotalMemory : Hardware
{
    private readonly object _processLock = new();
    private int _currentProcessId;

    public TotalMemory(ISettings settings)
        : base("Total Memory", new Identifier("ram"), settings)
    {
        PhysicalMemoryUsed = new Sensor("RAM Used", 0, SensorType.Data, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = "2_0" };
        ActivateSensor(PhysicalMemoryUsed);

        PhysicalMemoryAvailable = new Sensor("RAM Available", 1, SensorType.Data, this, settings)
        { PresentationSortKey = "2_1" };
        ActivateSensor(PhysicalMemoryAvailable);

        PhysicalMemoryLoad = new Sensor("RAM Usage", 0, SensorType.Load, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = "2_2" };
        ActivateSensor(PhysicalMemoryLoad);

        PhysicalMemoryGameUsed = new Sensor("RAM Game Used", 4, SensorType.Data, this, settings)
        { PresentationSortKey = "2_3" };
        ActivateSensor(PhysicalMemoryGameUsed);

        if (ProcessServiceProvider.ProcessService != null)
        {
            ProcessServiceProvider.ProcessService
                .ProcessIdStream
                .DistinctUntilChanged()
                .Subscribe(processId =>
                {
                    lock (_processLock)
                    {
                        _currentProcessId = processId;

                        if (!Software.OperatingSystem.IsUnix)
                        {
                            MemoryWindows.UpdateProcessCounter(this, processId);
                        }
                    }
                });
        }
    }

    public override HardwareType HardwareType => HardwareType.Memory;

    internal int CurrentProcessId
    {
        get { lock (_processLock) { return _currentProcessId; } }
    }

    internal Sensor PhysicalMemoryAvailable { get; }

    internal Sensor PhysicalMemoryGameUsed { get; }

    internal Sensor PhysicalMemoryLoad { get; }

    internal Sensor PhysicalMemoryUsed { get; }

    public override void Update()
    {
        if (Software.OperatingSystem.IsUnix)
        {
            MemoryLinux.Update(this);
        }
        else
        {
            MemoryWindows.Update(this);
        }
    }
}
