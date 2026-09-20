// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using CapFrameX.Service.Monitoring.Contracts;

namespace CapFrameX.Service.Monitoring.Hardware.Cpu;

internal abstract class AmdCpu(int processorIndex, CpuId[][] cpuId, ISettings settings, ISensorConfig sensorConfig = null)
    : GenericCpu(processorIndex, cpuId, settings, sensorConfig);
