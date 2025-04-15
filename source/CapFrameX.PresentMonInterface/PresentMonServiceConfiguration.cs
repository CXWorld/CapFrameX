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
        private const string PARAMETER_SEPARATOR = " ";

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
                arguments += PARAMETER_SEPARATOR;
                arguments += "--stop_existing_session";
                arguments += PARAMETER_SEPARATOR;
                arguments += "--output_stdout";
                arguments += PARAMETER_SEPARATOR;
                arguments += "--no_track_input";
                arguments += PARAMETER_SEPARATOR;
                arguments += "--qpc_time_ms";
                // w/o FrameType, it's flawed when using XeFG
                //arguments += PARAMETER_SEPARATOR;
                //arguments += "--track_frame_type";

                if (ExcludeProcesses != null && ExcludeProcesses.Any())
                {
                    foreach (var process in ExcludeProcesses.Where(proc => !proc.Contains(" ")))
                    {
                        arguments += PARAMETER_SEPARATOR;
                        arguments += "--exclude";
                        arguments += PARAMETER_SEPARATOR;
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
                arguments += PARAMETER_SEPARATOR;
                arguments += "--stop_existing_session";
                arguments += PARAMETER_SEPARATOR;
                arguments += "--process_name";
                arguments += PARAMETER_SEPARATOR;
                arguments += ProcessName;
                arguments += PARAMETER_SEPARATOR;
                arguments += "--output_file";
                arguments += PARAMETER_SEPARATOR;
                arguments += OutputFilename;
                arguments += PARAMETER_SEPARATOR;
                arguments += "--no_track_input";
                arguments += PARAMETER_SEPARATOR;
                arguments += "--qpc_time_ms";
                // w/o FrameType, it's flawed when using XeFG
                //arguments += PARAMETER_SEPARATOR;
                //arguments += "--track_frame_type";
            }

            return arguments;
        }
    }
}
