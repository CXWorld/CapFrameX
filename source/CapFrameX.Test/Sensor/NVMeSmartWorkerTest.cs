using LibreHardwareMonitor.Hardware.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32.Storage.Nvme;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class NVMeSmartWorkerTest
    {
        [TestMethod]
        public void HealthReadRunsSingleFlightAndCallerStaysNonBlocking()
        {
            var drive = new BlockingNvmeDrive();
            var handle = new SafeFileHandle(new IntPtr(1), ownsHandle: false);
            var smart = new NVMeSmart(2, handle, drive);

            try
            {
                smart.RequestHealthInfo();
                Assert.IsTrue(drive.ReadStarted.WaitOne(1000), "The health worker did not start its read.");

                var stopwatch = Stopwatch.StartNew();
                NVMeHealthInfo initial = smart.GetHealthInfo();
                stopwatch.Stop();

                Assert.IsNull(initial, "No cache entry should be published before the hardware read completes.");
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500, "Reading the cache blocked on the hardware worker.");

                for (int i = 0; i < 10; i++)
                    smart.RequestHealthInfo();

                Assert.AreEqual(1, drive.HealthReadCount, "More than one NVMe request was allowed in flight.");

                drive.AllowCompletion.Set();

                bool cacheAvailable = SpinWait.SpinUntil(
                    () =>
                    {
                        NVMeHealthInfo health;
                        TimeSpan age;
                        return smart.TryGetHealthInfo(out health, out age);
                    },
                    2000);

                Assert.IsTrue(cacheAvailable, "The completed health read was not published to the cache.");

                smart.RequestHealthInfo();
                Thread.Sleep(100);
                Assert.AreEqual(1, drive.HealthReadCount, "A fresh cache entry caused another hardware read.");
            }
            finally
            {
                drive.AllowCompletion.Set();
                smart.Close();
                handle.Dispose();
                drive.Dispose();
            }
        }

        [TestMethod]
        public void CloseCancelsPendingHealthRead()
        {
            var drive = new CancellableBlockingNvmeDrive();
            var handle = new SafeFileHandle(new IntPtr(1), ownsHandle: false);
            var smart = new NVMeSmart(2, handle, drive);

            try
            {
                smart.RequestHealthInfo();
                Assert.IsTrue(drive.ReadStarted.WaitOne(1000), "The health worker did not start its read.");

                var stopwatch = Stopwatch.StartNew();
                smart.Close();
                stopwatch.Stop();

                Assert.AreEqual(1, drive.CancellationCount, "Closing did not cancel the active NVMe request.");
                Assert.IsTrue(drive.ReadCompleted.WaitOne(0), "The health worker did not leave the cancelled read.");
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "Closing waited for the worker timeout instead of cancelling the request.");
            }
            finally
            {
                drive.AllowCompletion.Set();
                smart.Close();
                handle.Dispose();
                drive.Dispose();
            }
        }

        private sealed class BlockingNvmeDrive : INVMeDrive, IDisposable
        {
            private int _healthReadCount;

            public ManualResetEvent AllowCompletion { get; } = new ManualResetEvent(false);

            public int HealthReadCount => Volatile.Read(ref _healthReadCount);

            public ManualResetEvent ReadStarted { get; } = new ManualResetEvent(false);

            public void Dispose()
            {
                AllowCompletion.Dispose();
                ReadStarted.Dispose();
            }

            public bool HealthInfoLog(SafeHandle hDevice, out NVME_HEALTH_INFO_LOG data)
            {
                Interlocked.Increment(ref _healthReadCount);
                ReadStarted.Set();
                AllowCompletion.WaitOne();
                data = new NVME_HEALTH_INFO_LOG();
                return true;
            }

            public SafeHandle Identify(StorageInfo storageInfo)
            {
                return null;
            }

            public bool IdentifyController(SafeHandle hDevice, out NVME_IDENTIFY_CONTROLLER_DATA data)
            {
                data = new NVME_IDENTIFY_CONTROLLER_DATA();
                return false;
            }
        }

        private sealed class CancellableBlockingNvmeDrive : INVMeDrive, ICancellableNVMeDrive, IDisposable
        {
            private int _cancellationCount;

            public ManualResetEvent AllowCompletion { get; } = new ManualResetEvent(false);

            public int CancellationCount => Volatile.Read(ref _cancellationCount);

            public ManualResetEvent ReadCompleted { get; } = new ManualResetEvent(false);

            public ManualResetEvent ReadStarted { get; } = new ManualResetEvent(false);

            public void CancelPendingIo()
            {
                Interlocked.Increment(ref _cancellationCount);
                AllowCompletion.Set();
            }

            public void Dispose()
            {
                AllowCompletion.Dispose();
                ReadCompleted.Dispose();
                ReadStarted.Dispose();
            }

            public bool HealthInfoLog(SafeHandle hDevice, out NVME_HEALTH_INFO_LOG data)
            {
                ReadStarted.Set();
                AllowCompletion.WaitOne();
                data = new NVME_HEALTH_INFO_LOG();
                ReadCompleted.Set();
                return true;
            }

            public SafeHandle Identify(StorageInfo storageInfo)
            {
                return null;
            }

            public bool IdentifyController(SafeHandle hDevice, out NVME_IDENTIFY_CONTROLLER_DATA data)
            {
                data = new NVME_IDENTIFY_CONTROLLER_DATA();
                return false;
            }
        }
    }
}
