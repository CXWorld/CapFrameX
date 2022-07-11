using CapFrameX.Monitoring.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenHardwareMonitor.Hardware.IntelGPU
{
    internal class IntelGPU : GPUBase
    {
        private readonly int adapterIndex;
        private readonly int busNumber;
        private readonly int deviceNumber;
        private readonly ISensorConfig sensorConfig;

        public IntelGPU(string name, int adapterIndex, int busNumber,
          int deviceNumber, ISettings settings, ISensorConfig config, IProcessService processService)
          : base(name, new Identifier("intelgpu",
            adapterIndex.ToString(CultureInfo.InvariantCulture)), settings, processService)
        {
            this.adapterIndex = adapterIndex;
            this.busNumber = busNumber;
            this.deviceNumber = deviceNumber;
            this.sensorConfig = config;
        }

        public override HardwareType HardwareType => HardwareType.GpuIntel;

        public int DeviceNumber => deviceNumber;
        public int BusNumber => busNumber;

        public override void Update()
        {
            throw new NotImplementedException();
        }
    }
}
