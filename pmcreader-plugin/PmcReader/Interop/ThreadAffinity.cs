// From LibreHardwareMonitor
// Mozilla Public License 2.0
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors
// All Rights Reserved

using System;

namespace PmcReader.Interop
{
    internal static class ThreadAffinity
    {
        public static ulong Set(ulong mask)
        {
            if (mask == 0)
                return 0;

            UIntPtr uIntPtrMask;
            try
            {
                uIntPtrMask = (UIntPtr)mask;
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(mask));
            }
            return (ulong)Kernel32.SetThreadAffinityMask(Kernel32.GetCurrentThread(), uIntPtrMask);
        }
    }
}

