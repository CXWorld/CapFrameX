using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using SharpDX.DXGI;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class SystemInfoTest
    {
        [TestMethod]
        public void CheckGPUInfo_CorrectMemoryData()
        {
            using (var factory = new Factory4())
            {
                var adapter = factory.GetAdapter(0).QueryInterface<Adapter3>();
                var description = adapter.Description.DedicatedVideoMemory;
                var size = (long)description;
                QueryVideoMemoryInformation info = adapter.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
            }

            bool exists = PerformanceCounterCategory.Exists("GPU Adapter Memory");

            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var instances = category.GetInstanceNames();

            PerformanceCounter dedicatedUsage = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instances.Last());
            PerformanceCounter shardUsage = new PerformanceCounter("GPU Adapter Memory", "Shared Usage", instances.Last());
            PerformanceCounter totalCommitted = new PerformanceCounter("GPU Adapter Memory", "Total Committed", instances.Last());

            while (true)
            {
                Thread.Sleep(1000);
                Console.WriteLine(dedicatedUsage.RawValue);
                Console.WriteLine(shardUsage.RawValue);
                Console.WriteLine(totalCommitted.RawValue);
            }
        }
    }
}
