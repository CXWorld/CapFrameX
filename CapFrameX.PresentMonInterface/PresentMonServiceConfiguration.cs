using CapFrameX.Contracts.PresentMonInterface;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CapFrameX.PresentMonInterface
{
    public class PresentMonServiceConfiguration : ICaptureServiceConfiguration
    {
        public string ProcessName { get; set; }

        public bool RedirectOutputStream { get; set; }

        /// <summary>
        /// verbose or simple
        /// </summary>
        public string OutputLevelofDetail { get; set; } = "verbose";

        public bool CaptureAllProcesses { get; set; } = false;

        public string OutputFilename { get; set; }

        public List<string> ExcludeProcesses { get; set; }

        public string ConfigParameterToArguments()
        {
            if (string.IsNullOrWhiteSpace(ProcessName))
            {
                throw new ArgumentException("Output filename must be set!");
            }

            if (!CaptureAllProcesses && string.IsNullOrWhiteSpace(ProcessName))
            {
                throw new ArgumentException("Process name must be set!");
            }

            var arguments = string.Empty;
            if (CaptureAllProcesses)
            {
                arguments += "-captureall";
                arguments += " ";
                arguments += "-multi_csv";
                arguments += " ";
                arguments += "-output_file";
                arguments += " ";
                arguments += OutputFilename;
                if (!string.IsNullOrWhiteSpace(OutputLevelofDetail))
                {
                    arguments += " ";
                    arguments += "-" + OutputLevelofDetail;
                }
                if (ExcludeProcesses != null && ExcludeProcesses.Any())
                {
                    arguments += " ";
                    foreach (var process in ExcludeProcesses)
                    {
                        arguments += "-exclude";
                        arguments += " ";
                        arguments += process;
                    }
                }
            }
            else
            {
                if (RedirectOutputStream)
                {
                    arguments += "-process_name";
                    arguments += " ";
                    arguments += ProcessName;
                    arguments += " ";
                    // ToDo: edit here, when function is been provided
                    arguments += "-redirect_output";
                    if (!string.IsNullOrWhiteSpace(OutputLevelofDetail))
                    {
                        arguments += " ";
                        arguments += "-" + OutputLevelofDetail;
                    }
                }
                else
                {
                    arguments += "-process_name";
                    arguments += " ";
                    arguments += ProcessName;
                    arguments += " ";
                    arguments += "-output_file";
                    arguments += " ";
                    arguments += OutputFilename;
                    if (!string.IsNullOrWhiteSpace(OutputLevelofDetail))
                    {
                        arguments += " ";
                        arguments += "-" + OutputLevelofDetail;
                    }
                }
            }

            return arguments;
        }
    }
}
