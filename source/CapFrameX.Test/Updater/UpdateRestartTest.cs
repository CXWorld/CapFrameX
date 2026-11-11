using CapFrameX.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CapFrameX.Test.Updater
{
	/// <summary>
	/// ping.exe stands in for the previous CapFrameX instance: it runs for as many seconds as it is
	/// told to and can be identified by its executable path.
	/// </summary>
	[TestClass]
	public class UpdateRestartTest
	{
		private static readonly string Ping = Path.Combine(Environment.SystemDirectory, "PING.EXE");

		[TestMethod]
		public void CreateStartInfo_PassesTheProcessIdThroughTheShell()
		{
			var startInfo = UpdateRestart.CreateStartInfo(@"C:\Program Files\CapFrameX\CapFrameX.exe", 4242);

			Assert.IsTrue(startInfo.UseShellExecute, "Shell execution passes on no handles of the exiting instance.");
			Assert.AreEqual("--install-update-after 4242", startInfo.Arguments);
			Assert.AreEqual(@"C:\Program Files\CapFrameX", startInfo.WorkingDirectory);
			Assert.IsTrue(UpdateRestart.TryGetProcessId(startInfo.Arguments.Split(' '), out var processId));
			Assert.AreEqual(4242, processId);
		}

		[TestMethod]
		public void TryGetProcessId_AcceptsOnlyTheExactArgumentWithAPositiveId()
		{
			var invalid = new[]
			{
				null,
				new string[0],
				new[] { "--install-update-after" },
				new[] { "--install-update-after", "abc" },
				new[] { "--install-update-after", "-1" },
				new[] { "--install-update-after", "+42" },
				new[] { "--install-update-after", "0" },
				new[] { "--INSTALL-UPDATE-AFTER", "42" }
			};

			foreach (var args in invalid)
				Assert.IsFalse(UpdateRestart.TryGetProcessId(args, out _), string.Join(" ", args ?? new string[0]));
		}

		[TestMethod]
		public void WaitForPreviousInstance_WithoutArgument_ReturnsAtOnce()
		{
			Assert.IsNull(UpdateRestart.WaitForPreviousInstance(new[] { "-minimized" }, Ping, TimeSpan.FromSeconds(30)));
		}

		[TestMethod]
		public void WaitForPreviousInstance_ExitingInstance_IsAwaited()
		{
			using (var previous = StartPing(seconds: 2))
			{
				var result = UpdateRestart.WaitForPreviousInstance(Args(previous), Ping, TimeSpan.FromSeconds(30));

				Assert.IsTrue(previous.HasExited, result);
				StringAssert.Contains(result, "has exited");
			}
		}

		[TestMethod]
		public void WaitForPreviousInstance_HangingInstance_IsEndedAfterTheGracePeriod()
		{
			using (var previous = StartPing(seconds: 60))
			{
				var result = UpdateRestart.WaitForPreviousInstance(Args(previous), Ping, TimeSpan.FromMilliseconds(500));

				Assert.IsTrue(previous.HasExited, "A hanging shutdown must not block the installation: " + result);
				StringAssert.Contains(result, "has been ended");
			}
		}

		[TestMethod]
		public void WaitForPreviousInstance_OtherExecutable_IsNeitherAwaitedNorEnded()
		{
			using (var other = StartPing(seconds: 60))
			{
				try
				{
					var result = UpdateRestart.WaitForPreviousInstance(Args(other),
						@"C:\Program Files\CapFrameX\CapFrameX.exe", TimeSpan.FromMilliseconds(500));

					Assert.IsFalse(other.HasExited, "The argument must not end a process that is not CapFrameX.");
					StringAssert.Contains(result, "does not run");
				}
				finally
				{
					other.Kill();
				}
			}
		}

		[TestMethod]
		public void WaitForPreviousInstance_ExitedInstance_ContinuesAtOnce()
		{
			// The handle stays open until the check, so the process ID cannot be reused meanwhile.
			using (var previous = StartPing(seconds: 1))
			{
				previous.WaitForExit();

				var result = UpdateRestart.WaitForPreviousInstance(Args(previous), Ping, TimeSpan.FromSeconds(30));

				StringAssert.Contains(result, "already exited");
			}
		}

		private static string[] Args(Process previous)
			=> new[] { UpdateRestart.WaitForExitArgument, previous.Id.ToString(CultureInfo.InvariantCulture) };

		private static Process StartPing(int seconds)
			=> Process.Start(new ProcessStartInfo(Ping, $"-n {seconds} 127.0.0.1")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true
			});
	}
}
