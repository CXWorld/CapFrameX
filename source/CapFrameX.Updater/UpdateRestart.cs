using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Hands a staged update over to a new instance of CapFrameX. The running instance cannot start
	/// the installer itself: the installer replaces its files, and the shutdown - PresentMon, sensors,
	/// overlay - takes a moment and can hang. So it starts a new instance with
	/// <see cref="WaitForExitArgument"/> and its process ID, then exits; the new instance waits until
	/// the old one is gone and takes the regular start path, which installs the staged package.
	/// </summary>
	public static class UpdateRestart
	{
		public const string WaitForExitArgument = "--install-update-after";

		/// <summary>
		/// How long the new instance waits before it ends the old one. A normal shutdown takes a few
		/// seconds; the AMD ADLX teardown can deadlock and never finish.
		/// </summary>
		public static readonly TimeSpan ShutdownGracePeriod = TimeSpan.FromSeconds(30);

		private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(5);

		public static ProcessStartInfo CreateStartInfo(string executablePath, int processId)
			=> new ProcessStartInfo(executablePath)
			{
				// Shell execution keeps this process's elevation but, unlike CreateProcess from .NET,
				// passes on none of its handles, which could otherwise outlive this process.
				UseShellExecute = true,
				Arguments = WaitForExitArgument + " " + processId.ToString(CultureInfo.InvariantCulture),
				WorkingDirectory = Path.GetDirectoryName(executablePath)
			};

		/// <summary>
		/// Waits for the instance named by <see cref="WaitForExitArgument"/> and ends it once the
		/// grace period has passed. Only a process running <paramref name="ownExecutablePath"/> is
		/// waited for or ended, so the argument cannot be turned against any other process.
		/// </summary>
		/// <returns>A line for the log, which does not exist yet at this point; null without the argument.</returns>
		public static string WaitForPreviousInstance(string[] args, string ownExecutablePath, TimeSpan gracePeriod)
		{
			if (!TryGetProcessId(args, out var processId))
				return null;

			Process previous;
			try
			{
				previous = Process.GetProcessById(processId);
			}
			catch (ArgumentException)
			{
				return $"The previous instance {processId} had already exited.";
			}

			using (previous)
			{
				try
				{
					if (previous.HasExited)
						return $"The previous instance {processId} had already exited.";

					if (!IsSameExecutable(previous, ownExecutablePath))
						return $"Process {processId} does not run {ownExecutablePath}, so this instance does not wait for it.";

					if (previous.WaitForExit((int)gracePeriod.TotalMilliseconds))
						return $"The previous instance {processId} has exited.";

					previous.Kill();
					previous.WaitForExit((int)KillTimeout.TotalMilliseconds);
					return $"The previous instance {processId} was still running after {gracePeriod.TotalSeconds:0} s and has been ended.";
				}
				catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception || ex is NotSupportedException)
				{
					return $"Unable to wait for the previous instance {processId}: {ex.Message}";
				}
			}
		}

		internal static bool TryGetProcessId(string[] args, out int processId)
		{
			processId = 0;
			if (args == null)
				return false;

			var index = Array.IndexOf(args, WaitForExitArgument);
			return index >= 0
				&& index + 1 < args.Length
				&& int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out processId)
				&& processId > 0;
		}

		private static bool IsSameExecutable(Process process, string executablePath)
		{
			if (string.IsNullOrWhiteSpace(executablePath))
				return false;

			var processPath = GetExecutablePath(process);
			return !string.IsNullOrWhiteSpace(processPath)
				&& string.Equals(Path.GetFullPath(processPath), Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Process.MainModule enumerates the modules of the other process, which fails while that
		/// process is still starting; the image name is available from the first moment.
		/// </summary>
		private static string GetExecutablePath(Process process)
		{
			var buffer = new StringBuilder(32768);
			var length = buffer.Capacity;
			return QueryFullProcessImageName(process.SafeHandle, 0, buffer, ref length)
				? buffer.ToString(0, length)
				: null;
		}

		[DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool QueryFullProcessImageName(SafeProcessHandle process, int flags,
			StringBuilder executableName, ref int length);
	}
}
