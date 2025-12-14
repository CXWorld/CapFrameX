// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware;

/// <summary>
/// Category of what type the selected sensor is.
/// </summary>
public enum SensorType
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Voltage, // V
    Current, // A
    Power, // W
    Clock, // MHz
    Temperature, // °C
    Load, // %
    Frequency, // Hz
    Fan, // RPM
    Flow, // L/h
    Control, // %
    Level, // %
    Factor, // 1
    Data, // GB = 2^30 Bytes
    SmallData, // MB = 2^20 Bytes
    Throughput, // B/s
    TimeSpan, // Seconds
    Timing, // ns
    Energy, // milliwatt-hour (mWh)
    Noise, // dBA
    Conductivity, // µS/cm
    Humidity // %
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// Stores the readed value and the time in which it was recorded.
/// </summary>
public struct SensorValue
{
    /// <param name="value"><see cref="Value"/> of the sensor.</param>
    /// <param name="time">The time code during which the <see cref="Value"/> was recorded.</param>
    public SensorValue(float value, DateTime time)
    {
        Value = value;
        Time = time;
    }

    /// <summary>
    /// Gets the value of the sensor
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Gets the time code during which the <see cref="Value"/> was recorded.
    /// </summary>
    public DateTime Time { get; }
}

/// <summary>
/// Stores information about the readed values and the time in which they were collected.
/// </summary>
public interface ISensor : IElement
{
    /// <summary>
    /// <inheritdoc cref="IControl"/>
    /// </summary>
    IControl Control { get; }

    /// <summary>
    /// <inheritdoc cref="IHardware"/>
    /// </summary>
    IHardware Hardware { get; }

    /// <summary>
    /// Gets the unique identifier of this sensor.
    /// </summary>
    Identifier Identifier { get; }

    /// <summary>
    /// Gets the unique identifier of this sensor for a given <see cref="IHardware"/>.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets a value indicating whether the sensor is hidden by default in the user interface.
    /// </summary>
    bool IsDefaultHidden { get; }

    /// <summary>
    /// Gets a maximum value recorded for the given sensor.
    /// </summary>
    float? Max { get; }

    /// <summary>
    /// Gets a minimum value recorded for the given sensor.
    /// </summary>
    float? Min { get; }

    /// <summary>
    /// Gets or sets a sensor name.
    /// <para>By default determined by the library.</para>
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Gets a list of parameters for the given sensor.
    /// </summary>
    IReadOnlyList<IParameter> Parameters { get; }

    /// <summary>
    /// <inheritdoc cref="LibreHardwareMonitor.Hardware.SensorType"/>
    /// </summary>
    SensorType SensorType { get; }

    /// <summary>
    /// Gets the last recorded value for the given sensor.
    /// </summary>
    float? Value { get; }

    /// <summary>
    /// Gets a value indicating whether the sensor is used as default presentation in the user interface (overlay etc.).
    /// </summary>
    bool IsPresentationDefault { get; }

    /// <summary>
    /// Gets a list of recorded values for the given sensor.
    /// </summary>
    IEnumerable<SensorValue> Values { get; }

    /// <summary>
    /// Gets or sets the time window for which the values are stored in <see cref="Values"/>.
    /// </summary>
    TimeSpan ValuesTimeWindow { get; set; }

    /// <summary>
    /// Resets a value stored in <see cref="Min"/>.
    /// </summary>
    void ResetMin();

    /// <summary>
    /// Resets a value stored in <see cref="Max"/>.
    /// </summary>
    void ResetMax();

    /// <summary>
    /// Clears the values stored in <see cref="Values"/>.
    /// </summary>
    void ClearValues();
}
