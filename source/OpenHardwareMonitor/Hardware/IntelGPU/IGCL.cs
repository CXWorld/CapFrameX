using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenHardwareMonitor.Hardware.IntelGPU
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IGCLAdapterInfo
    {
        public int AdapterIndex { get; internal set; }
        public string LUID { get; internal set; }
        public int VendorID { get; internal set; }
        public int BusNumber { get; internal set; }
        public int DeviceNumber { get; internal set; }
        public string AdapterName { get; internal set; }
    }

    internal class IGCL
    {
        public static int ADL_OK { get; internal set; }
        public static int Intel_VENDOR_ID = 0x8086;
        public static bool IsInitialized { get; internal set; }

        internal static int CtlInit()
        {
            throw new NotImplementedException();
        }

        internal static void ADL_Adapter_NumberOfAdapters_Get(ref int numberOfAdapters)
        {
            throw new NotImplementedException();
        }

        internal static void ADL_Adapter_Active_Get(object adapterIndex, out int isActive)
        {
            throw new NotImplementedException();
        }

        internal static void ADL_Adapter_ID_Get(object adapterIndex, out int adapterID)
        {
            throw new NotImplementedException();
        }

        internal static int ADL_Adapter_AdapterInfo_Get(IGCLAdapterInfo[] adapterInfo)
        {
            throw new NotImplementedException();
        }

        internal static void CtlClose()
        {
            throw new NotImplementedException();
        }
    }
}
