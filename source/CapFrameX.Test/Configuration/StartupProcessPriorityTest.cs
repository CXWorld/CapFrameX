using CapFrameX.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class StartupProcessPriorityTest
    {
        [TestMethod]
        // The Task Scheduler's default for a task (priority 7 and 8).
        [DataRow(ProcessPriorityClass.BelowNormal)]
        // Task priority 9 and 10.
        [DataRow(ProcessPriorityClass.Idle)]
        public void APriorityBelowNormalIsRaisedToNormal(ProcessPriorityClass priorityClass)
        {
            Assert.AreEqual(ProcessPriorityClass.Normal, StartupProcessPriority.RaisedPriority(priorityClass));
        }

        [TestMethod]
        [DataRow(ProcessPriorityClass.Normal)]
        [DataRow(ProcessPriorityClass.AboveNormal)]
        [DataRow(ProcessPriorityClass.High)]
        [DataRow(ProcessPriorityClass.RealTime)]
        public void ANormalOrHigherPriorityIsKept(ProcessPriorityClass priorityClass)
        {
            Assert.IsNull(StartupProcessPriority.RaisedPriority(priorityClass));
        }

        [TestMethod]
        public void TheCurrentProcessStartedBelowNormalRunsAtNormalPriority()
        {
            using var process = Process.GetCurrentProcess();
            ProcessPriorityClass original = process.PriorityClass;
            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;

                Assert.AreEqual(ProcessPriorityClass.BelowNormal, StartupProcessPriority.RaiseCurrentProcess());
                process.Refresh();
                Assert.AreEqual(ProcessPriorityClass.Normal, process.PriorityClass);

                // Nothing left to raise.
                Assert.IsNull(StartupProcessPriority.RaiseCurrentProcess());
            }
            finally
            {
                process.PriorityClass = original;
            }
        }
    }
}
