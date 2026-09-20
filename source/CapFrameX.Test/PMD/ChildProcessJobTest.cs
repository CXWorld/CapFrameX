using System;
using System.Diagnostics;
using System.IO;
using CapFrameX.PMD.Benchlab;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PMD
{
    [TestClass]
    public class ChildProcessJobTest
    {
        [TestMethod]
        public void Dispose_TerminatesAssignedProcess()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "ping.exe"),
                Arguments = "127.0.0.1 -n 30",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                Assert.IsNotNull(process);

                try
                {
                    using (ChildProcessJob.Attach(process))
                    {
                    }

                    Assert.IsTrue(process.WaitForExit(2000));
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                    }
                }
            }
        }
    }
}
