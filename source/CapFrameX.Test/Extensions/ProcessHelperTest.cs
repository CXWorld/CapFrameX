using CapFrameX.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace CapFrameX.Test.Extensions
{
    [TestClass]
    public class ProcessHelperTest
    {
        [TestMethod]
        public void IsProcessAlive_TheCurrentProcessIsAlive()
        {
            using var current = Process.GetCurrentProcess();

            Assert.IsTrue(ProcessHelper.IsProcessAlive(current.Id));
        }

        [TestMethod]
        public void IsProcessAlive_AProcessThatDeniesTheQueryIsAlive()
        {
            // csrss is protected: it denies QueryInformation even to administrators and every
            // query to other users. The process list used to drop such a process on every pass.
            Process[] csrss = Process.GetProcessesByName("csrss");
            try
            {
                Assert.IsTrue(csrss.Length > 0, "Windows always runs csrss.");
                Assert.IsTrue(ProcessHelper.IsProcessAlive(csrss[0].Id));
            }
            finally
            {
                foreach (Process process in csrss) process.Dispose();
            }
        }

        [TestMethod]
        public void IsProcessAlive_AnExitedProcessIsNotAlive()
        {
            var start = new ProcessStartInfo("cmd.exe", "/c exit 3")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var process = Process.Start(start);
            Assert.IsTrue(process.WaitForExit(10_000));

            // The handle this object holds keeps the PID known, so the exit code decides.
            Assert.IsFalse(ProcessHelper.IsProcessAlive(process.Id));
        }

        [TestMethod]
        public void IsProcessAlive_APidWindowsDoesNotKnowIsNotAlive()
        {
            // OpenProcess fails with ERROR_INVALID_PARAMETER, the answer for an exited process.
            Assert.IsFalse(ProcessHelper.IsProcessAlive(0x7FFFFFFC));
        }

        [TestMethod]
        public void TryGetImagePath_ReadsThePathWithoutModuleAccess()
        {
            // Call of Duty's Ricochet anti-cheat denies MainModule even to administrators
            // (2026-10-25); the limited query right it grants is enough for the path.
            using var current = Process.GetCurrentProcess();

            Assert.IsTrue(ProcessHelper.TryGetImagePath(current.Id, out string path));
            Assert.AreEqual(current.MainModule.FileName, path, ignoreCase: true);
            Assert.IsFalse(ProcessHelper.TryGetImagePath(0x7FFFFFFC, out _));
        }

        [TestMethod]
        public void GetFileDescription_ReadsTheExecutable()
        {
            using var current = Process.GetCurrentProcess();

            Assert.AreEqual(current.MainModule.FileVersionInfo.FileDescription?.TrimEnd(),
                ProcessHelper.GetFileDescription(current.Id));
            Assert.IsNull(ProcessHelper.GetFileDescription(0x7FFFFFFC));
        }
    }
}
