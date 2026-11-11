using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Localization;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Overlay;
using Prism.Events;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX
{
    /// <summary>
    /// Offers to register the Vulkan layers at the start of the portable version, which writes no
    /// registry values on its own (<see cref="PortableVulkanLayerCheck"/>). The script runs with the
    /// rights of CapFrameX, which always runs elevated, so it needs no UAC prompt
    /// (<see cref="VulkanLayerRegistrationScript"/>). A declined offer comes back only once the
    /// layers of this folder or their registration change.
    /// </summary>
    internal static class PortableVulkanLayerPrompt
    {
        private static readonly TimeSpan ScriptTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Starts on the UI thread; the checks and the script run on the thread pool.
        /// </summary>
        public static async Task RunAsync(IAppConfiguration appConfiguration, IEventAggregator eventAggregator)
        {
            try
            {
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = PortableVulkanLayerCheck.GetScriptPath(appDirectory);

                var assessment = await Task.Run(() => Assess(appDirectory));
                if (assessment == null || assessment.Action == PortableVulkanLayerAction.None)
                    return;

                if (string.Equals(assessment.Signature, appConfiguration.DeclinedPortableVulkanLayerSignature, StringComparison.Ordinal))
                {
                    Log.Logger.Information("Portable Vulkan layer: {Action} needed, declined before for these registrations.", assessment.Action);
                    return;
                }

                if (!File.Exists(scriptPath))
                {
                    Log.Logger.Warning("Portable Vulkan layer: {Action} needed, but {Script} is missing.", assessment.Action, scriptPath);
                    return;
                }

                Log.Logger.Information("Portable Vulkan layer: {Action} needed. Registered now:{NewLine}{Detail}",
                    assessment.Action, Environment.NewLine, assessment.Detail);

                string layerFolder = Path.GetDirectoryName(scriptPath);
                string question = assessment.Action == PortableVulkanLayerAction.Install
                    ? CxLang.Format("PortableVulkanLayerPrompt_Install01", layerFolder, PortableVulkanLayerCheck.ScriptFileName)
                    : CxLang.Format("PortableVulkanLayerPrompt_Update012", assessment.Detail, layerFolder, PortableVulkanLayerCheck.ScriptFileName);

                if (MessageBox.Show(question, CxLang.T("PortableVulkanLayerPrompt_Title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    appConfiguration.DeclinedPortableVulkanLayerSignature = assessment.Signature;
                    Log.Logger.Information("Portable Vulkan layer: the user declined the registration.");
                    return;
                }

                var result = await Task.Run(() => VulkanLayerRegistrationScript.Run(scriptPath, ScriptTimeout));
                var after = await Task.Run(() => Assess(appDirectory));
                bool registered = result.ExitCode == 0 && after?.Action == PortableVulkanLayerAction.None;

                // Also after a failure: a timed-out script may have changed some of the values.
                eventAggregator.GetEvent<PubSubEvent<AppMessages.VulkanLayerRegistrationChanged>>()
                    .Publish(new AppMessages.VulkanLayerRegistrationChanged());

                string report = string.Join(Environment.NewLine,
                    new[] { result.Output, result.Error, "Registered now:", after?.Detail }
                        .Where(part => !string.IsNullOrWhiteSpace(part))
                        .Select(part => part.TrimEnd()));

                if (registered)
                {
                    Log.Logger.Information("Portable Vulkan layer: registered.{NewLine}{Report}", Environment.NewLine, report);
                    MessageBox.Show(CxLang.T("PortableVulkanLayerPrompt_Registered"), CxLang.T("PortableVulkanLayerPrompt_Title"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Log.Logger.Warning("Portable Vulkan layer: registration failed (exit code {ExitCode}, timed out {TimedOut}).{NewLine}{Report}",
                    result.ExitCode, result.TimedOut, Environment.NewLine, report);

                string reason = result.TimedOut
                    ? CxLang.Format("PortableVulkanLayerPrompt_ScriptTimedOut0", (int)ScriptTimeout.TotalSeconds)
                    : LastLine(result.Error) ?? after?.Detail ?? $"exit code {result.ExitCode}";
                MessageBox.Show(CxLang.Format("PortableVulkanLayerPrompt_Failed0", reason), CxLang.T("PortableVulkanLayerPrompt_Title"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while checking the Vulkan layer registration of the portable version.");
            }
        }

        private static PortableVulkanLayerAssessment Assess(string appDirectory)
        {
            var assessment = PortableVulkanLayerCheck.Assess(appDirectory, out string problem);
            if (assessment == null)
                Log.Logger.Warning("Portable Vulkan layer: not checked, {Problem}.", problem);
            return assessment;
        }

        private static string LastLine(string text)
            => text?.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .LastOrDefault(line => line.Length > 0);
    }
}
