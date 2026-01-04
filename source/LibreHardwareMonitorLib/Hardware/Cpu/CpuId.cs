// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Cpu;

/// <summary>
/// Represents the CPU vendor.
/// </summary>
public enum Vendor
{
    /// <summary>
    /// Unknown vendor.
    /// </summary>
    Unknown,

    /// <summary>
    /// Intel Corporation.
    /// </summary>
    Intel,

    /// <summary>
    /// Advanced Micro Devices (AMD).
    /// </summary>
    AMD
}

/// <summary>
/// Represents the type of CPU core in hybrid architectures.
/// </summary>
public enum CpuCoreType
{
    /// <summary>
    /// Non-hybrid core type.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Performance core (P-core) - Intel and AMD.
    /// </summary>
    PerformanceCore = 1,

    /// <summary>
    /// Efficiency core (E-core) - Intel.
    /// </summary>
    EfficiencyCore = 2,

    /// <summary>
    /// Low-Power Efficiency core (LPE-core) - Intel.
    /// </summary>
    LowPowerEfficiencyCore = 3,

    /// <summary>
    /// Dense core (D-core) - AMD.
    /// </summary>
    DenseCore = 4
}

/// <summary>
/// Represents CPUID information for a single logical processor.
/// </summary>
public class CpuId
{
#pragma warning disable CS3003 // Type is not CLS-compliant
    /// <summary>
    /// The base CPUID function number.
    /// </summary>
    public const uint CPUID_0 = 0;

    /// <summary>
    /// The extended CPUID function number base.
    /// </summary>
    public const uint CPUID_EXT = 0x80000000;
#pragma warning restore CS3003 // Type is not CLS-compliant

    private readonly int _group;
    private readonly int _thread;
    private readonly GroupAffinity _affinity;

    private readonly Vendor _vendor = Vendor.Unknown;

    private readonly string _cpuBrandString = string.Empty;
    private readonly string _name = string.Empty;

    private readonly uint[,] _cpuidData = new uint[0, 0];
    private readonly uint[,] _cpuidExtData = new uint[0, 0];

    private readonly uint _family;
    private readonly uint _model;
    private readonly uint _stepping;
    private readonly uint _pkgType;

    private readonly uint _apicId;

    private readonly uint _threadMaskWith;
    private readonly uint _coreMaskWith;

    private readonly uint _processorId;
    private readonly uint _coreId;
    private readonly uint _threadId;

    private static void AppendRegister(StringBuilder b, uint value)
    {
        b.Append((char)((value) & 0xff));
        b.Append((char)((value >> 8) & 0xff));
        b.Append((char)((value >> 16) & 0xff));
        b.Append((char)((value >> 24) & 0xff));
    }

    private static uint NextLog2(long x)
    {
        if (x <= 0)
            return 0;

        x--;
        uint count = 0;
        while (x > 0)
        {
            x >>= 1;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Gets the CPUID information for the specified processor group and thread.
    /// </summary>
    /// <param name="group">The processor group index.</param>
    /// <param name="thread">The thread index within the group.</param>
    /// <returns>A <see cref="CpuId"/> instance, or <c>null</c> if the thread could not be accessed.</returns>
    public static CpuId Get(int group, int thread)
    {
        if (thread >= 64)
            return null;

        var affinity = GroupAffinity.Single((ushort)group, thread);

        var previousAffinity = ThreadAffinity.Set(affinity);
        if (previousAffinity == GroupAffinity.Undefined)
            return null;

        try
        {
            return new CpuId(group, thread, affinity);
        }
        finally
        {
            ThreadAffinity.Set(previousAffinity);
        }
    }

    private CpuId(int group, int thread, GroupAffinity affinity)
    {
        this._group = group;
        this._thread = thread;
        this._affinity = affinity;

        uint maxCpuid = 0;
        uint maxCpuidExt = 0;

        uint eax, ebx, ecx, edx;

        OpCode.CpuId(CPUID_0, 0, out eax, out ebx, out ecx, out edx);
        if (eax > 0)
            maxCpuid = eax;
        else
            return;

        StringBuilder vendorBuilder = new StringBuilder();
        AppendRegister(vendorBuilder, ebx);
        AppendRegister(vendorBuilder, edx);
        AppendRegister(vendorBuilder, ecx);
        string cpuVendor = vendorBuilder.ToString();
        switch (cpuVendor)
        {
            case "GenuineIntel":
                _vendor = Vendor.Intel;
                break;
            case "AuthenticAMD":
                _vendor = Vendor.AMD;
                break;
            default:
                _vendor = Vendor.Unknown;
                break;
        }
        eax = ebx = ecx = edx = 0;
        OpCode.CpuId(CPUID_EXT, 0, out eax, out ebx, out ecx, out edx);
        if (eax > CPUID_EXT)
            maxCpuidExt = eax - CPUID_EXT;
        else
            return;

        maxCpuid = Math.Min(maxCpuid, 1024);
        maxCpuidExt = Math.Min(maxCpuidExt, 1024);

        _cpuidData = new uint[maxCpuid + 1, 4];
        for (uint i = 0; i < (maxCpuid + 1); i++)
            OpCode.CpuId(CPUID_0 + i, 0,
              out _cpuidData[i, 0], out _cpuidData[i, 1],
              out _cpuidData[i, 2], out _cpuidData[i, 3]);

        _cpuidExtData = new uint[maxCpuidExt + 1, 4];
        for (uint i = 0; i < (maxCpuidExt + 1); i++)
            OpCode.CpuId(CPUID_EXT + i, 0,
              out _cpuidExtData[i, 0], out _cpuidExtData[i, 1],
              out _cpuidExtData[i, 2], out _cpuidExtData[i, 3]);

        StringBuilder nameBuilder = new StringBuilder();
        for (uint i = 2; i <= 4; i++)
        {
            OpCode.CpuId(CPUID_EXT + i, 0, out eax, out ebx, out ecx, out edx);
            AppendRegister(nameBuilder, eax);
            AppendRegister(nameBuilder, ebx);
            AppendRegister(nameBuilder, ecx);
            AppendRegister(nameBuilder, edx);
        }
        nameBuilder.Replace('\0', ' ');
        _cpuBrandString = nameBuilder.ToString().Trim();
        nameBuilder.Replace("Dual-Core Processor", "");
        nameBuilder.Replace("Triple-Core Processor", "");
        nameBuilder.Replace("Quad-Core Processor", "");
        nameBuilder.Replace("Six-Core Processor", "");
        nameBuilder.Replace("Eight-Core Processor", "");
        nameBuilder.Replace("Dual Core Processor", "");
        nameBuilder.Replace("Quad Core Processor", "");
        nameBuilder.Replace("12-Core Processor", "");
        nameBuilder.Replace("16-Core Processor", "");
        nameBuilder.Replace("24-Core Processor", "");
        nameBuilder.Replace("32-Core Processor", "");
        nameBuilder.Replace("64-Core Processor", "");
        nameBuilder.Replace("6-Core Processor", "");
        nameBuilder.Replace("8-Core Processor", "");
        nameBuilder.Replace("with Radeon Vega Mobile Gfx", "");
        nameBuilder.Replace("w/ Radeon Vega Mobile Gfx", "");
        nameBuilder.Replace("with Radeon Vega Graphics", "");
        nameBuilder.Replace("APU with Radeon(tm) HD Graphics", "");
        nameBuilder.Replace("APU with Radeon(TM) HD Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R2 Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R3 Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R4 Graphics", "");
        nameBuilder.Replace("APU with AMD Radeon R5 Graphics", "");
        nameBuilder.Replace("APU with Radeon(tm) R3", "");
        nameBuilder.Replace("RADEON R2, 4 COMPUTE CORES 2C+2G", "");
        nameBuilder.Replace("RADEON R4, 5 COMPUTE CORES 2C+3G", "");
        nameBuilder.Replace("RADEON R5, 5 COMPUTE CORES 2C+3G", "");
        nameBuilder.Replace("RADEON R5, 10 COMPUTE CORES 4C+6G", "");
        nameBuilder.Replace("RADEON R7, 10 COMPUTE CORES 4C+6G", "");
        nameBuilder.Replace("RADEON R7, 12 COMPUTE CORES 4C+8G", "");
        nameBuilder.Replace("Radeon R5, 6 Compute Cores 2C+4G", "");
        nameBuilder.Replace("Radeon R5, 8 Compute Cores 4C+4G", "");
        nameBuilder.Replace("Radeon R6, 10 Compute Cores 4C+6G", "");
        nameBuilder.Replace("Radeon R7, 10 Compute Cores 4C+6G", "");
        nameBuilder.Replace("Radeon R7, 12 Compute Cores 4C+8G", "");
        nameBuilder.Replace("R5, 10 Compute Cores 4C+6G", "");
        nameBuilder.Replace("R7, 12 COMPUTE CORES 4C+8G", "");
        nameBuilder.Replace("(R)", " ");
        nameBuilder.Replace("(TM)", " ");
        nameBuilder.Replace("(tm)", " ");
        nameBuilder.Replace("CPU", " ");

        for (int i = 0; i < 10; i++) nameBuilder.Replace("  ", " ");
        _name = nameBuilder.ToString();
        if (_name.Contains("@"))
            _name = _name.Remove(_name.LastIndexOf('@'));
        _name = _name.Trim();

        this._family = ((_cpuidData[1, 0] & 0x0FF00000) >> 20) +
          ((_cpuidData[1, 0] & 0x0F00) >> 8);
        this._model = ((_cpuidData[1, 0] & 0x0F0000) >> 12) +
          ((_cpuidData[1, 0] & 0xF0) >> 4);
        this._stepping = (_cpuidData[1, 0] & 0x0F);
        this._pkgType = (_cpuidExtData[1, 1] >> 28) & 0xFF;

        this._apicId = (_cpuidData[1, 1] >> 24) & 0xFF;

        switch (_vendor)
        {
            case Vendor.Intel:
                uint maxCoreAndThreadIdPerPackage = (_cpuidData[1, 1] >> 16) & 0xFF;
                uint maxCoreIdPerPackage;
                if (maxCpuid >= 4)
                    maxCoreIdPerPackage = ((_cpuidData[4, 0] >> 26) & 0x3F) + 1;
                else
                    maxCoreIdPerPackage = 1;
                _threadMaskWith =
                  NextLog2(maxCoreAndThreadIdPerPackage / maxCoreIdPerPackage);
                _coreMaskWith = NextLog2(maxCoreIdPerPackage);
                break;
            case Vendor.AMD:
                if (this._family == 0x17 || this._family == 0x19 || this._family == 0x1A)
                {
                    _coreMaskWith = (_cpuidExtData[8, 2] >> 12) & 0xF;
                    _threadMaskWith =
                      NextLog2(((_cpuidExtData[0x1E, 1] >> 8) & 0xFF) + 1);
                }
                else
                {
                    uint corePerPackage;
                    if (maxCpuidExt >= 8)
                        corePerPackage = (_cpuidExtData[8, 2] & 0xFF) + 1;
                    else
                        corePerPackage = 1;
                    _coreMaskWith = NextLog2(corePerPackage);
                    _threadMaskWith = 0;
                }
                break;
            default:
                _threadMaskWith = 0;
                _coreMaskWith = 0;
                break;
        }

        _processorId = (_apicId >> (int)(_coreMaskWith + _threadMaskWith));
        _coreId = ((_apicId >> (int)(_threadMaskWith))
          - (_processorId << (int)(_coreMaskWith)));
        _threadId = _apicId
          - (_processorId << (int)(_coreMaskWith + _threadMaskWith))
          - (_coreId << (int)(_threadMaskWith));
    }

    /// <summary>
    /// Gets the processor name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the full CPU brand string as reported by CPUID.
    /// </summary>
    public string BrandString => _cpuBrandString;

    /// <summary>
    /// Gets the processor group index.
    /// </summary>
    public int Group => _group;

    /// <summary>
    /// Gets the thread index within the processor group.
    /// </summary>
    public int Thread => _thread;

    /// <summary>
    /// Gets the processor affinity for this logical processor.
    /// </summary>
    public GroupAffinity Affinity => _affinity;

    /// <summary>
    /// Gets the CPU vendor.
    /// </summary>
    public Vendor Vendor => _vendor;

    /// <summary>
    /// Gets the CPU family identifier.
    /// </summary>
    public uint Family => _family;

    /// <summary>
    /// Gets the CPU model identifier.
    /// </summary>
    public uint Model => _model;

    /// <summary>
    /// Gets the CPU stepping revision.
    /// </summary>
    public uint Stepping => _stepping;

    /// <summary>
    /// Gets the package type identifier.
    /// </summary>
    public uint PkgType => _pkgType;

    /// <summary>
    /// Gets the APIC identifier for this logical processor.
    /// </summary>
    public uint ApicId => _apicId;

    /// <summary>
    /// Gets the physical processor identifier.
    /// </summary>
    public uint ProcessorId => _processorId;

    /// <summary>
    /// Gets the core identifier within the processor.
    /// </summary>
    public uint CoreId => _coreId;

    /// <summary>
    /// Gets the thread identifier within the core.
    /// </summary>
    public uint ThreadId => _threadId;

    /// <summary>
    /// Gets the raw CPUID data for standard functions.
    /// </summary>
    public uint[,] Data => _cpuidData;

    /// <summary>
    /// Gets the raw CPUID data for extended functions.
    /// </summary>
    public uint[,] ExtData => _cpuidExtData;
}

//public class CpuId
//{
//    /// <summary>
//    /// Initializes a new instance of the <see cref="CpuId" /> class.
//    /// </summary>
//    /// <param name="group">The group.</param>
//    /// <param name="thread">The thread.</param>
//    /// <param name="affinity">The affinity.</param>
//    private CpuId(int group, int thread, GroupAffinity affinity)
//    {
//        Thread = thread;
//        Group = group;
//        Affinity = affinity;

//        uint threadMaskWith;
//        uint coreMaskWith;
//        uint maxCpuidExt;

//        if (thread >= 64)
//            throw new ArgumentOutOfRangeException(nameof(thread));

//        uint maxCpuid;
//        if (OpCode.CpuId(CPUID_0, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
//        {
//            if (eax > 0)
//                maxCpuid = eax;
//            else
//                return;

//            StringBuilder vendorBuilder = new();
//            AppendRegister(vendorBuilder, ebx);
//            AppendRegister(vendorBuilder, edx);
//            AppendRegister(vendorBuilder, ecx);

//            Vendor = vendorBuilder.ToString() switch
//            {
//                "GenuineIntel" => Vendor.Intel,
//                "AuthenticAMD" => Vendor.AMD,
//                _ => Vendor.Unknown
//            };

//            if (OpCode.CpuId(CPUID_EXT, 0, out eax, out _, out _, out _))
//            {
//                if (eax > CPUID_EXT)
//                    maxCpuidExt = eax - CPUID_EXT;
//                else
//                    return;
//            }
//            else
//            {
//                throw new ArgumentOutOfRangeException(nameof(thread));
//            }
//        }
//        else
//        {
//            throw new ArgumentOutOfRangeException(nameof(thread));
//        }

//        maxCpuid = Math.Min(maxCpuid, 1024);
//        maxCpuidExt = Math.Min(maxCpuidExt, 1024);

//        Data = new uint[maxCpuid + 1, 4];
//        for (uint i = 0; i < maxCpuid + 1; i++)
//        {
//            OpCode.CpuId(CPUID_0 + i, 0, out Data[i, 0], out Data[i, 1], out Data[i, 2], out Data[i, 3]);
//        }

//        ExtData = new uint[maxCpuidExt + 1, 4];
//        for (uint i = 0; i < maxCpuidExt + 1; i++)
//        {
//            OpCode.CpuId(CPUID_EXT + i, 0, out ExtData[i, 0], out ExtData[i, 1], out ExtData[i, 2], out ExtData[i, 3]);
//        }

//        StringBuilder nameBuilder = new();
//        for (uint i = 2; i <= 4; i++)
//        {
//            if (OpCode.CpuId(CPUID_EXT + i, 0, out eax, out ebx, out ecx, out edx))
//            {
//                AppendRegister(nameBuilder, eax);
//                AppendRegister(nameBuilder, ebx);
//                AppendRegister(nameBuilder, ecx);
//                AppendRegister(nameBuilder, edx);
//            }
//        }

//        nameBuilder.Replace('\0', ' ');
//        BrandString = nameBuilder.ToString().Trim();
//        nameBuilder.Replace("(R)", string.Empty);
//        nameBuilder.Replace("(TM)", string.Empty);
//        nameBuilder.Replace("(tm)", string.Empty);
//        nameBuilder.Replace("CPU", string.Empty);
//        nameBuilder.Replace("Dual-Core Processor", string.Empty);
//        nameBuilder.Replace("Triple-Core Processor", string.Empty);
//        nameBuilder.Replace("Quad-Core Processor", string.Empty);
//        nameBuilder.Replace("Six-Core Processor", string.Empty);
//        nameBuilder.Replace("Eight-Core Processor", string.Empty);
//        nameBuilder.Replace("64-Core Processor", string.Empty);
//        nameBuilder.Replace("32-Core Processor", string.Empty);
//        nameBuilder.Replace("24-Core Processor", string.Empty);
//        nameBuilder.Replace("16-Core Processor", string.Empty);
//        nameBuilder.Replace("12-Core Processor", string.Empty);
//        nameBuilder.Replace("8-Core Processor", string.Empty);
//        nameBuilder.Replace("6-Core Processor", string.Empty);

//        for (int i = 0; i < 10; i++)
//            nameBuilder.Replace("  ", " ");

//        Name = nameBuilder.ToString();
//        if (Name.Contains("@"))
//            Name = Name.Remove(Name.LastIndexOf('@'));

//        Name = Name.Trim();
//        Family = ((Data[1, 0] & 0x0FF00000) >> 20) + ((Data[1, 0] & 0x0F00) >> 8);
//        Model = ((Data[1, 0] & 0x0F0000) >> 12) + ((Data[1, 0] & 0xF0) >> 4);
//        Stepping = Data[1, 0] & 0x0F;
//        ApicId = (Data[1, 1] >> 24) & 0xFF;
//        PkgType = (ExtData[1, 1] >> 28) & 0xFF;

//        switch (Vendor)
//        {
//            case Vendor.Intel:
//                uint maxCoreAndThreadIdPerPackage = (Data[1, 1] >> 16) & 0xFF;
//                uint maxCoreIdPerPackage;
//                if (maxCpuid >= 4)
//                    maxCoreIdPerPackage = ((Data[4, 0] >> 26) & 0x3F) + 1;
//                else
//                    maxCoreIdPerPackage = 1;

//                threadMaskWith = NextLog2(maxCoreAndThreadIdPerPackage / maxCoreIdPerPackage);
//                coreMaskWith = NextLog2(maxCoreIdPerPackage);
//                break;
//            case Vendor.AMD:
//                uint corePerPackage;
//                if (maxCpuidExt >= 8)
//                    corePerPackage = (ExtData[8, 2] & 0xFF) + 1;
//                else
//                    corePerPackage = 1;

//                threadMaskWith = 0;
//                coreMaskWith = NextLog2(corePerPackage);

//                if (Family is 0x17 or 0x19)
//                {
//                    // ApicIdCoreIdSize: APIC ID size.
//                    // cores per DIE
//                    // we need this for Ryzen 5 (4 cores, 8 threads) ans Ryzen 6 (6 cores, 12 threads)
//                    // Ryzen 5: [core0][core1][dummy][dummy][core2][core3] (Core0 EBX = 00080800, Core2 EBX = 08080800)
//                    coreMaskWith = ((ExtData[8, 2] >> 12) & 0xF) switch
//                    {
//                        0x04 => NextLog2(16), // Ryzen
//                        0x05 => NextLog2(32), // Threadripper
//                        0x06 => NextLog2(64), // Epic
//                        _ => coreMaskWith
//                    };
//                }

//                break;
//            default:
//                threadMaskWith = 0;
//                coreMaskWith = 0;
//                break;
//        }

//        ProcessorId = ApicId >> (int)(coreMaskWith + threadMaskWith);
//        CoreId = (ApicId >> (int)threadMaskWith) - (ProcessorId << (int)coreMaskWith);
//        ThreadId = ApicId - (ProcessorId << (int)(coreMaskWith + threadMaskWith)) - (CoreId << (int)threadMaskWith);
//    }

//    public GroupAffinity Affinity { get; }

//    public uint ApicId { get; }

//    public string BrandString { get; } = string.Empty;

//    public uint CoreId { get; }

//    public uint[,] Data { get; } = new uint[0, 0];

//    public uint[,] ExtData { get; } = new uint[0, 0];

//    public uint Family { get; }

//    public int Group { get; }

//    public uint Model { get; }

//    public string Name { get; } = string.Empty;

//    public uint PkgType { get; }

//    public uint ProcessorId { get; }

//    public uint Stepping { get; }

//    public int Thread { get; }

//    public uint ThreadId { get; }

//    public Vendor Vendor { get; } = Vendor.Unknown;

//    /// <summary>
//    /// Gets the specified <see cref="CpuId" />.
//    /// </summary>
//    /// <param name="group">The group.</param>
//    /// <param name="thread">The thread.</param>
//    /// <returns><see cref="CpuId" />.</returns>
//    public static CpuId Get(int group, int thread)
//    {
//        if (thread >= 64)
//            return null;

//        var affinity = GroupAffinity.Single((ushort)group, thread);

//        GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);
//        if (previousAffinity == GroupAffinity.Undefined)
//            return null;

//        try
//        {
//            return new CpuId(group, thread, affinity);
//        }
//        finally
//        {
//            ThreadAffinity.Set(previousAffinity);
//        }
//    }

//    private static void AppendRegister(StringBuilder b, uint value)
//    {
//        b.Append((char)(value & 0xff));
//        b.Append((char)((value >> 8) & 0xff));
//        b.Append((char)((value >> 16) & 0xff));
//        b.Append((char)((value >> 24) & 0xff));
//    }

//    private static uint NextLog2(long x)
//    {
//        if (x <= 0)
//            return 0;

//        x--;
//        uint count = 0;
//        while (x > 0)
//        {
//            x >>= 1;
//            count++;
//        }

//        return count;
//    }

//    // ReSharper disable InconsistentNaming
//    public const uint CPUID_0 = 0;
//    public const uint CPUID_EXT = 0x80000000;
//    // ReSharper restore InconsistentNaming
//}
