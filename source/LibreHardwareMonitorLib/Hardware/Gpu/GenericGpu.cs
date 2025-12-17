// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using CapFrameX.Monitoring.Contracts;
using System.Reactive.Linq;
using System;
using System.Diagnostics;

namespace LibreHardwareMonitor.Hardware.Gpu;

/// <summary>
/// Represents a generic GPU device.
/// </summary>
public abstract class GenericGpu : Hardware
{
    protected readonly object _displayLock = new();
    protected RefreshRateBuffer<float> _refreshRateBuffer;
    protected Display _display;
    protected float _refreshRateCurrentWindowHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericGpu" /> class.
    /// </summary>
    /// <param name="name">Component name.</param>
    /// <param name="identifier">Identifier that will be assigned to the device. Based on <see cref="Identifier" /></param>
    /// <param name="settings">Additional settings passed by the <see cref="IComputer" />.</param>
    protected GenericGpu(string name, Identifier identifier, ISettings settings) : base(name, identifier, settings)
    {
        _refreshRateBuffer = new RefreshRateBuffer<float>(2);

        ProcessServiceProvider.ProcessService
            .ProcessIdStream
            .DistinctUntilChanged()
            .Subscribe(id =>
            {
                lock (_displayLock)
                {
                    _refreshRateBuffer.Clear();

                    if (id == 0)
                    {
                        _display = null;
                        _refreshRateCurrentWindowHandle = 0;
                    }
                    else
                    {
                        try
                        {
                            var process = Process.GetProcessById(id);
                            _display = new Display(process.MainWindowHandle);
                            _refreshRateCurrentWindowHandle = _display.GetDisplayRefreshRate();
                        }
                        catch
                        {
                            _display = null;
                            _refreshRateCurrentWindowHandle = 0;
                        }
                    }
                }
            });
    }

    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    public abstract string DeviceId { get; }

    /// <summary>
    /// Gets a value indicating whether the GPU is a discrete GPU.
    /// </summary>
    public bool IsDiscreteGpu { get; set; } = true;
}