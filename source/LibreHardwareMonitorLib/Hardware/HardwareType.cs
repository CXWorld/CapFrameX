// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

namespace LibreHardwareMonitor.Hardware;

/// <summary>
/// Collection of identifiers representing the purpose of the hardware.
/// </summary>
public enum HardwareType
{
    /// <summary>
    /// The mainboard.
    /// </summary>
    Motherboard,

    /// <summary>
    /// A Super I/O chip on the mainboard.
    /// </summary>
    SuperIO,

    /// <summary>
    /// A processor.
    /// </summary>
    Cpu,

    /// <summary>
    /// The system memory.
    /// </summary>
    Memory,

    /// <summary>
    /// An NVIDIA graphics card.
    /// </summary>
    GpuNvidia,

    /// <summary>
    /// An AMD graphics card.
    /// </summary>
    GpuAmd,

    /// <summary>
    /// An Intel graphics card.
    /// </summary>
    GpuIntel,

    /// <summary>
    /// A storage device.
    /// </summary>
    Storage,

    /// <summary>
    /// A network adapter.
    /// </summary>
    Network,

    /// <summary>
    /// A cooling device.
    /// </summary>
    Cooler,

    /// <summary>
    /// An embedded controller on the mainboard.
    /// </summary>
    EmbeddedController,

    /// <summary>
    /// A power supply unit.
    /// </summary>
    Psu,

    /// <summary>
    /// A battery.
    /// </summary>
    Battery
}