using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using SharpDX.DXGI;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Management;

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

            var dedicatedUsages = instances
                    .Select(instance => new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instance))
                    .Select((u, i) => (dedicatedUsage: u, Index: i));

            var maxUsage = dedicatedUsages.Max(tuple => (tuple.dedicatedUsage, tuple.Index));

            PerformanceCounter dedicatedUsage = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instances.First());
            PerformanceCounter shardUsage = new PerformanceCounter("GPU Adapter Memory", "Shared Usage", instances.First());
            PerformanceCounter totalCommitted = new PerformanceCounter("GPU Adapter Memory", "Total Committed", instances.First());

            while (true)
            {
                Thread.Sleep(1000);
                Console.WriteLine(dedicatedUsage.NextValue());
                Console.WriteLine(shardUsage.NextValue());
                Console.WriteLine(totalCommitted.NextValue());
            }
        }

        [TestMethod]
        public void CheckDeviceId_CorrectId()
        {
            // Win32_VideoController
            var win32DeviceClassName = "Win32_VideoController";
            // var win32DeviceClassName = "Win32_DisplayConfiguration";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            using (var searcher = new ManagementObjectSearcher(query))
            {
                ManagementObjectCollection objectCollection = searcher.Get();

                foreach (ManagementBaseObject managementBaseObject in objectCollection)
                {
                    foreach (PropertyData propertyData in managementBaseObject.Properties)
                    {
                        Console.WriteLine($"Name: {propertyData.Name}, Value: {propertyData.Value}");
                    }
                }
            }
        }
    }
}
