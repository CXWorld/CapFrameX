using System;
using System.Linq;
using System.Collections.Generic;

namespace CapFrameX.PresentMonInterface
{
    /// <summary>
    /// Documentation: https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md
    /// </summary>
    public class PresentMonServiceConfiguration
    {
        public string ProcessName { get; set; }

        public bool RedirectOutputStream { get; set; }

        public string OutputFilename { get; set; }

        public List<string> ExcludeProcesses { get; set; }

        public string ConfigParameterToArguments()
        {
            var arguments = string.Empty;
            if (RedirectOutputStream)
            {
                arguments += "--restart_as_admin";
                arguments += " ";
                arguments += "--stop_existing_session";
                arguments += " ";
                arguments += "--output_stdout";
                arguments += " ";
                arguments += "--no_track_input";
                arguments += " ";
                arguments += "--qpc_time_ms";

                if (ExcludeProcesses != null && ExcludeProcesses.Any())
                {
                    foreach (var process in ExcludeProcesses.Where(proc => !proc.Contains(" ")))
                    {
                        arguments += " ";
                        arguments += "--exclude";
                        arguments += " ";
                        arguments += process + ".exe";
                    }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(ProcessName))
                {
                    throw new ArgumentException("Process name must be set!");
                }

                arguments += "--restart_as_admin";
                arguments += " ";
                arguments += "--stop_existing_session";
                arguments += " ";
                arguments += "--process_name";
                arguments += " ";
                arguments += ProcessName;
                arguments += " ";
                arguments += "--output_file";
                arguments += " ";
                arguments += OutputFilename;
                arguments += " ";
                arguments += "--no_track_input";
                arguments += " ";
                arguments += "--qpc_time_ms";
            }

            return arguments;
        }
    }
}
