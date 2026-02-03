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
    private const int DISPLAY_CHECK_INTERVAL_MS = 2000;

    /// <summary>
    /// Lock object for display-related operations.
    /// </summary>
    protected readonly object _displayLock = new();
    /// <summary>
    /// Buffer to store refresh rate values.
    /// </summary>
    protected RefreshRateBuffer<float> _refreshRateBuffer;
    /// <summary>
    /// Current display associated with the GPU.
    /// </summary>
    protected Display _display;
    /// <summary>
    /// Current refresh rate of the window handle.
    /// </summary>
    protected float _refreshRateCurrentDisplay;
    /// <summary>
    /// Name of the display device. 
    /// </summary>
    protected string _displayDeviceName;

    private int _currentProcessId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericGpu" /> class.
    /// </summary>
    /// <param name="name">Component name.</param>
    /// <param name="identifier">Identifier that will be assigned to the device. Based on <see cref="Identifier" /></param>
    /// <param name="settings">Additional settings passed by the <see cref="IComputer" />.</param>
    protected GenericGpu(string name, Identifier identifier, ISettings settings) : base(name, identifier, settings)
    {
        _refreshRateBuffer = new RefreshRateBuffer<float>(2);

        var processChangeStream = ProcessServiceProvider.ProcessService
            .ProcessIdStream
            .DistinctUntilChanged()
            .Select(id => (ProcessId: id, IsProcessChange: true));

        var periodicCheckStream = Observable
            .Interval(TimeSpan.FromMilliseconds(DISPLAY_CHECK_INTERVAL_MS))
            .Select(_ => (ProcessId: _currentProcessId, IsProcessChange: false));

        processChangeStream
            .Merge(periodicCheckStream)
            .Subscribe(args =>
            {
                lock (_displayLock)
                {
                    if (args.IsProcessChange)
                    {
                        _currentProcessId = args.ProcessId;
                    }

                    if (args.ProcessId == 0)
                    {
                        if (_display != null)
                        {
                            _refreshRateBuffer.Clear();
                        }

                        _display = null;
                        _refreshRateCurrentDisplay = 0;
                        _displayDeviceName = null;
                    }
                    else
                    {
                        try
                        {
                            var process = Process.GetProcessById(args.ProcessId);
                            var newDisplay = new Display(process.MainWindowHandle);
                            var newDisplayDeviceName = newDisplay.GetDisplayDeviceName();

                            if (_displayDeviceName != newDisplayDeviceName)
                            {
                                _refreshRateBuffer.Clear();
                                _displayDeviceName = newDisplayDeviceName;
                            }

                            _display = newDisplay;
                            _refreshRateCurrentDisplay = _display.GetDisplayRefreshRate();
                        }
                        catch
                        {
                            if (_display != null)
                            {
                                _refreshRateBuffer.Clear();
                            }

                            _display = null;
                            _refreshRateCurrentDisplay = 0;
                            _displayDeviceName = null;
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
