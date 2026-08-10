// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware.Storage;

/// <summary>
/// Describes a single SMART attribute and how it is turned into a sensor value.
/// </summary>
public class SmartAttribute
{
    private readonly RawValueConversion _rawValueConversion;

    /// <summary>
    /// Converts the raw bytes of a SMART attribute into a sensor value.
    /// </summary>
    /// <param name="rawValue">The six raw bytes of the attribute.</param>
    /// <param name="value">The normalized current value of the attribute.</param>
    /// <param name="parameters">The parameters of the sensor.</param>
    /// <returns>The converted value.</returns>
    public delegate float RawValueConversion(byte[] rawValue, byte value, IReadOnlyList<IParameter> parameters);

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartAttribute" /> class.
    /// </summary>
    /// <param name="id">The SMART id of the attribute.</param>
    /// <param name="name">The name of the attribute.</param>
    public SmartAttribute(byte id, string name) : this(id, name, null, null, 0, null)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartAttribute" /> class.
    /// </summary>
    /// <param name="id">The SMART id of the attribute.</param>
    /// <param name="name">The name of the attribute.</param>
    /// <param name="rawValueConversion">
    /// A delegate for converting the raw byte
    /// array into a value (or null to use the attribute value).
    /// </param>
    public SmartAttribute(byte id, string name, RawValueConversion rawValueConversion) : this(id, name, rawValueConversion, null, 0, null)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmartAttribute" /> class.
    /// </summary>
    /// <param name="id">The SMART id of the attribute.</param>
    /// <param name="name">The name of the attribute.</param>
    /// <param name="rawValueConversion">
    /// A delegate for converting the raw byte
    /// array into a value (or null to use the attribute value).
    /// </param>
    /// <param name="sensorType">
    /// Type of the sensor or null if no sensor is to
    /// be created.
    /// </param>
    /// <param name="sensorChannel">
    /// If there exists more than one attribute with
    /// the same sensor channel and type, then a sensor is created only for the
    /// first attribute.
    /// </param>
    /// <param name="sensorName">
    /// The name to be used for the sensor, or null if
    /// no sensor is created.
    /// </param>
    /// <param name="defaultHiddenSensor">True to hide the sensor initially.</param>
    /// <param name="parameterDescriptions">
    /// Description for the parameters of the sensor
    /// (or null).
    /// </param>
    public SmartAttribute(byte id, string name, RawValueConversion rawValueConversion, SensorType? sensorType, int sensorChannel, string sensorName, bool defaultHiddenSensor = false, ParameterDescription[] parameterDescriptions = null)
    {
        Id = id;
        Name = name;
        _rawValueConversion = rawValueConversion;
        SensorType = sensorType;
        SensorChannel = sensorChannel;
        SensorName = sensorName;
        DefaultHiddenSensor = defaultHiddenSensor;
        ParameterDescriptions = parameterDescriptions;
    }

    /// <summary>
    /// Gets a value indicating whether the sensor is hidden initially.
    /// </summary>
    public bool DefaultHiddenSensor { get; }

    /// <summary>
    /// Gets a value indicating whether the raw value is converted instead of using the attribute value.
    /// </summary>
    public bool HasRawValueConversion => _rawValueConversion != null;

    /// <summary>
    /// Gets the SMART identifier.
    /// </summary>
    public byte Id { get; }

    /// <summary>
    /// Gets the name of the attribute.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the descriptions for the parameters of the sensor, or <see langword="null" /> if it has none.
    /// </summary>
    public ParameterDescription[] ParameterDescriptions { get; }

    /// <summary>
    /// Gets the channel of the sensor. Where several attributes share a channel and type, only the
    /// first one gets a sensor.
    /// </summary>
    public int SensorChannel { get; }

    /// <summary>
    /// Gets the name of the sensor, or <see langword="null" /> if no sensor is created.
    /// </summary>
    public string SensorName { get; }

    /// <summary>
    /// Gets the type of the sensor, or <see langword="null" /> if no sensor is created.
    /// </summary>
    public SensorType? SensorType { get; }

    internal unsafe float ConvertValue(AtaSmart.SMART_ATTRIBUTE value, IReadOnlyList<IParameter> parameters)
    {
        if (_rawValueConversion == null)
            return value.CurrentValue;

        Span<byte> rawValue = new(value.RawValue, 6);
        return _rawValueConversion(rawValue.ToArray(), value.CurrentValue, parameters);
    }
}
