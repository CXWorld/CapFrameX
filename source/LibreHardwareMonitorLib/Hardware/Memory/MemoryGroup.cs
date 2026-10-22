// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Monitoring.Contracts;
using RAMSPDToolkit.I2CSMBus;
using RAMSPDToolkit.SPD;
using RAMSPDToolkit.SPD.Enums;
using RAMSPDToolkit.SPD.Interop.Shared;
using RAMSPDToolkit.Windows.Driver;

namespace LibreHardwareMonitor.Hardware.Memory;

internal class MemoryGroup : IGroup, IHardwareChanged
{
    private static readonly object _lock = new();
    private List<Hardware> _hardware = [];

    private CancellationTokenSource _cancellationTokenSource;
    private Exception _lastException;
    private bool _disposed = false;
    private readonly ISensorConfig _sensorConfig;

    public MemoryGroup(ISettings settings, ISensorConfig sensorConfig = null)
    {
        _sensorConfig = sensorConfig;

        if (DriverManager.Driver is null || !DriverManager.Driver.IsOpen)
        {
            // Assign implementation of IDriver.
            DriverManager.Driver = new RAMSPDToolkitDriver();
            SMBusManager.UseWMI = false;
        }

        _hardware.Add(new VirtualMemory(settings));
        _hardware.Add(new TotalMemory(settings));

        if (DriverManager.Driver == null || !DriverManager.LoadDriver())
        {
            return;
        }

        if (!TryAddDimms(settings))
        {
            StartRetryTask(settings);
        }
    }

    public event HardwareEventHandler HardwareAdded;
    // DIMMs are only ever added, by the retry task; none is removed while the group lives.
    public event HardwareEventHandler HardwareRemoved { add { } remove { } }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport()
    {
        StringBuilder report = new();
        report.AppendLine("Memory Report:");
        if (_lastException != null)
        {
            report.AppendLine($"Error while detecting memory: {_lastException.Message}");
        }

        foreach (Hardware hardware in _hardware)
        {
            report.AppendLine($"{hardware.Name} ({hardware.Identifier}):");
            report.AppendLine();
            foreach (ISensor sensor in hardware.Sensors)
            {
                report.AppendLine($"{sensor.Name}: {sensor.Value?.ToString() ?? "No value"}");
            }
        }

        return report.ToString();
    }

    public void Close()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        lock (_lock)
        {
            foreach (Hardware ram in _hardware)
                ram.Close();

            _hardware = [];
            _disposed = true;
        }
    }

    private bool TryAddDimms(ISettings settings)
    {
        try
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return false;
                }

                if (DetectThermalSensors(out List<DetectedModule> modules))
                {
                    AddDimms(modules, settings);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _lastException = ex;
            Debug.Assert(false, "Exception while detecting RAM: " + ex.Message);
        }

        return false;
    }

    private void StartRetryTask(ISettings settings)
    {
        _cancellationTokenSource = new CancellationTokenSource();

        Task.Run(async () =>
        {
            int retryRemaining = 5;

            while (!_cancellationTokenSource.IsCancellationRequested && --retryRemaining > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2.5), _cancellationTokenSource.Token).ConfigureAwait(false);

                if (TryAddDimms(settings))
                {
                    break;
                }
            }
        }, _cancellationTokenSource.Token);
    }

    private readonly record struct DetectedModule(SPDAccessor Accessor, SMBusInterface Bus, byte Address);

    private static bool DetectThermalSensors(out List<DetectedModule> modules)
    {
        modules = [];

        bool ramDetected = false;

        SMBusManager.DetectSMBuses();

        //Go through detected SMBuses
        foreach (SMBusInterface smbus in SMBusManager.RegisteredSMBuses)
        {
            //Go through possible RAM slots
            for (byte i = SPDConstants.SPD_BEGIN; i <= SPDConstants.SPD_END; ++i)
            {
                //Detect type of RAM, if available
                SPDDetector detector = new(smbus, i);

                //RAM available and detected
                if (detector.Accessor != null)
                {
                    //Add all detected modules
                    modules.Add(new DetectedModule(detector.Accessor, smbus, i));

                    ramDetected = true;
                }
            }
        }

        return ramDetected;
    }

    private void AddDimms(List<DetectedModule> modules, ISettings settings)
    {
        List<Hardware> additions = [];

        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            SPDAccessor ram = modules[moduleIndex].Accessor;

            // Spread the modules' temperature reads evenly over the refresh interval, so one poll
            // never waits for more than one module.
            TimeSpan temperatureRefreshPhase = TimeSpan.FromTicks(DimmMemory.TemperatureRefreshInterval.Ticks * moduleIndex / modules.Count);

            //Default value
            string name = $"DIMM #{ram.Index}";
            string manufacturer = null;
            string partNumber = null;

            try
            {
                // Cache identity while the SPD page is already accessed for the display name.
                if (ram.ChangePage(PageData.ModulePartNumber))
                {
                    manufacturer = ram.GetModuleManufacturerString()?.Trim('\0', ' ', '\t', '\r', '\n');
                    partNumber = ram.ModulePartNumber()?.Trim('\0', ' ', '\t', '\r', '\n');
                }

                // Leave page 0 selected, as the toolkit's temperature read did on every poll; the
                // DDR5 temperature read in DimmMemory no longer touches the page.
                ram.ChangePage(PageData.ThermalData);
            }
            catch (Exception ex)
            {
                // A failed identity read must not prevent this or other DIMMs from being added.
                _lastException = ex;
            }

            if (!string.IsNullOrWhiteSpace(manufacturer) && !string.IsNullOrWhiteSpace(partNumber))
                name = $"{manufacturer} - {partNumber} (#{ram.Index})";
            else if (!string.IsNullOrWhiteSpace(manufacturer) || !string.IsNullOrWhiteSpace(partNumber))
                name = $"{(!string.IsNullOrWhiteSpace(manufacturer) ? manufacturer : partNumber)} (#{ram.Index})";

            DimmMemory memory = new(ram, modules[moduleIndex].Bus, modules[moduleIndex].Address, temperatureRefreshPhase, name, manufacturer, partNumber, new Identifier("memory", "dimm", $"{ram.Index}"), settings, _sensorConfig);
            additions.Add(memory);
        }

        _hardware = [.. _hardware, .. additions];
        additions.ForEach(hardware => HardwareAdded?.Invoke(hardware));
    }
}
