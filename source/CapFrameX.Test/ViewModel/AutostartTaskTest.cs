using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.Linq;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class AutostartTaskTest
    {
        private const string AppDirectory = @"C:\Program Files\CapFrameX\";
        private const string AppPath = @"C:\Program Files\CapFrameX\CapFrameX.exe";
        private const string UserId = @"PC\User";

        // A definition only; nothing is registered.
        private static TaskDefinition CreateDefinition(TaskService service)
        {
            TaskDefinition definition = service.NewTask();
            AutostartTask.Configure(definition, AppPath, AppDirectory, UserId);
            return definition;
        }

        [TestMethod]
        public void TheTaskStartsCapFrameXAtNormalPriority()
        {
            using var service = new TaskService();
            // The Task Scheduler's default, which PresentMon inherited from CapFrameX.
            Assert.AreEqual(ProcessPriorityClass.BelowNormal, service.NewTask().Settings.Priority);

            using TaskDefinition definition = CreateDefinition(service);

            Assert.AreEqual(ProcessPriorityClass.Normal, definition.Settings.Priority);
            StringAssert.Contains(definition.XmlText, "<Priority>5</Priority>");
        }

        [TestMethod]
        public void TheTaskDoesNotEndCapFrameXAfter72Hours()
        {
            using var service = new TaskService();
            Assert.AreEqual(TimeSpan.FromHours(72), service.NewTask().Settings.ExecutionTimeLimit);

            using TaskDefinition definition = CreateDefinition(service);

            Assert.AreEqual(TimeSpan.Zero, definition.Settings.ExecutionTimeLimit);
            // PT0S: the task may run indefinitely.
            StringAssert.Contains(definition.XmlText, "<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>");
        }

        [TestMethod]
        public void TheTaskStartsTheExecutableElevatedAfterTheLogon()
        {
            using var service = new TaskService();
            using TaskDefinition definition = CreateDefinition(service);

            Assert.AreEqual(TaskRunLevel.Highest, definition.Principal.RunLevel);
            Assert.IsFalse(definition.Settings.DisallowStartIfOnBatteries);
            Assert.IsFalse(definition.Settings.StopIfGoingOnBatteries);

            var trigger = definition.Triggers.OfType<LogonTrigger>().Single();
            Assert.AreEqual(UserId, trigger.UserId);
            Assert.AreEqual(AutostartTask.LogonDelay, trigger.Delay);

            var action = definition.Actions.OfType<ExecAction>().Single();
            Assert.AreEqual(AppPath, action.Path);
            Assert.AreEqual(AppDirectory, action.WorkingDirectory);
        }
    }
}
