// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) CapFrameX.Service.Monitoring and Contributors.
// All Rights Reserved.

using System;

namespace CapFrameX.Service.Monitoring.Hardware.Motherboard.Lpc.EC;

public interface IEmbeddedControllerIO : IDisposable
{
    void Read(ushort[] registers, byte[] data);
}