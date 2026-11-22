using System.Diagnostics;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Undoes a start below normal priority. The Task Scheduler starts a task at priority 7, below
    /// normal, unless the task names another one, and the autostart tasks of CapFrameX before 1.9.3
    /// named none. PresentMon inherits the priority class of CapFrameX, so under a game's load both
    /// fell behind and the hook-free charts stalled until CapFrameX was started by hand.
    /// </summary>
    public static class StartupProcessPriority
    {
        /// <summary>
        /// The priority class to run with instead of <paramref name="priorityClass"/>, or null to
        /// keep it: a class below normal is raised to normal, every other one stays.
        /// </summary>
        public static ProcessPriorityClass? RaisedPriority(ProcessPriorityClass priorityClass)
            => priorityClass == ProcessPriorityClass.BelowNormal || priorityClass == ProcessPriorityClass.Idle
                ? ProcessPriorityClass.Normal
                : null;

        /// <summary>
        /// Raises the current process to normal priority when it runs below it. Returns the class
        /// it ran with before, or null when it was kept. Call it before PresentMon is started.
        /// </summary>
        public static ProcessPriorityClass? RaiseCurrentProcess()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                ProcessPriorityClass current = process.PriorityClass;
                ProcessPriorityClass? raised = RaisedPriority(current);
                if (raised == null)
                    return null;

                process.PriorityClass = raised.Value;
                return current;
            }
        }
    }
}
