﻿// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Interop
{
    internal class NtDll
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
        {
            public long IdleTime;
            public long KernelTime;
            public long UserTime;
            public long DpcTime;
            public long InterruptTime;
            public uint InterruptCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_PROCESSOR_IDLE_INFORMATION
        {
            public long IdleTime;
            public long C1Time;
            public long C2Time;
            public long C3Time;
            public uint C1Transitions;
            public uint C2Transitions;
            public uint C3Transitions;
            public uint Padding;
        }

        internal enum SYSTEM_INFORMATION_CLASS
        {
            SystemProcessorPerformanceInformation = 8,
            SystemProcessorIdleInformation = 42
        }

        [DllImport("ntdll.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS SystemInformationClass, [Out] SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION[] SystemInformation, int SystemInformationLength, out int ReturnLength);

        [DllImport("ntdll.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS SystemInformationClass, [Out] SYSTEM_PROCESSOR_IDLE_INFORMATION[] SystemInformation, int SystemInformationLength, out int ReturnLength);
    }
}
