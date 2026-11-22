using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;

namespace CapFrameX.ViewModel
{
    /// <summary>
    /// The scheduled task that starts CapFrameX at logon.
    /// </summary>
    internal static class AutostartTask
    {
        /// <summary>
        /// The task's name in the root folder of the Task Scheduler.
        /// </summary>
        public const string Name = "CapFrameX";

        /// <summary>
        /// Time between the logon and the start of CapFrameX.
        /// </summary>
        public static readonly TimeSpan LogonDelay = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Fills a new task definition that starts <paramref name="appPath"/> elevated at the logon
        /// of <paramref name="userId"/>.
        /// </summary>
        public static void Configure(TaskDefinition definition, string appPath, string appDirectory, string userId)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            definition.RegistrationInfo.Description = "Autostart";
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            // A new task runs at priority 7, below normal, and PresentMon inherits it from CapFrameX.
            // Under a game's load both then fell behind: PresentMon delivered its rows late and in
            // waves, the hook-free charts stalled, and only a manual start (normal priority) helped.
            definition.Settings.Priority = ProcessPriorityClass.Normal;
            // A new task is ended after 72 hours, and CapFrameX with it.
            definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            definition.Principal.RunLevel = TaskRunLevel.Highest;

            definition.Triggers.Add(new LogonTrigger
            {
                UserId = userId,
                Delay = LogonDelay
            });
            definition.Actions.Add(new ExecAction(appPath, workingDirectory: appDirectory));
        }
    }
}
