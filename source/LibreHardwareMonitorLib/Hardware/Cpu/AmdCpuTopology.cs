// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware.Cpu;

internal sealed class AmdCpuTopology
{
    private const uint ExtendedTopologyLeaf = 0x80000026;

    internal AmdCpuTopology(uint ccdId, uint socketId)
    {
        CcdId = ccdId;
        SocketId = socketId;
    }

    internal uint CcdId { get; }

    internal uint SocketId { get; }

    // The caller must keep affinity pinned to the logical processor while reading.
    internal static AmdCpuTopology Read(Vendor vendor, uint maxExtendedLeaf, OpCode.CpuidDelegate readCpuid)
    {
        if (vendor != Vendor.AMD || maxExtendedLeaf < ExtendedTopologyLeaf)
            return null;

        uint? apicId = null;
        uint? ccdId = null;
        int previousShift = 0;

        // ECX[7:0] selects the level. Bound enumeration even for malformed firmware/VM data.
        for (uint subleaf = 0; subleaf < 256; subleaf++)
        {
            if (!readCpuid(ExtendedTopologyLeaf, subleaf, out uint eax, out uint ebx, out uint ecx, out uint edx))
                return null;

            uint levelType = (ecx >> 8) & 0xFF;
            if (levelType == 0 || (ebx & 0xFFFF) == 0)
                return null;

            int shift = (int)(eax & 0x1F);
            if (shift < previousShift || (apicId.HasValue && apicId.Value != edx))
                return null;

            apicId = edx;
            previousShift = shift;

            // AMD Fn8000_0026: the current level's MaskWidth removes all lower
            // hierarchy bits from the full Extended APIC ID. Types are 3 = CCD,
            // 4 = Socket; optional levels must not be inferred from subleaf indices.
            // https://github.com/tianocore/edk2/blob/master/MdePkg/Include/Register/Amd/Cpuid.h
            if (levelType == 3)
            {
                if (ccdId.HasValue)
                    return null;

                ccdId = edx >> shift;
            }
            else if (levelType == 4)
            {
                return ccdId.HasValue ? new AmdCpuTopology(ccdId.Value, edx >> shift) : null;
            }
        }

        return null;
    }

    internal static int? GetCcdCount(IEnumerable<AmdCpuTopology> topologies)
    {
        HashSet<uint> ccdIds = new();
        uint? socketId = null;

        foreach (AmdCpuTopology topology in topologies)
        {
            // Do not undercount partially available data or count merged sockets
            // if the caller's legacy processor grouping cannot distinguish them.
            if (topology == null || (socketId.HasValue && socketId.Value != topology.SocketId))
                return null;

            socketId = topology.SocketId;
            ccdIds.Add(topology.CcdId);
        }

        return ccdIds.Count > 0 ? ccdIds.Count : null;
    }
}
