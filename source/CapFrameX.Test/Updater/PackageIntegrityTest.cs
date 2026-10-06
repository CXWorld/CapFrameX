using CapFrameX.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Updater
{
	[TestClass]
	public class PackageIntegrityTest
	{
		[DataTestMethod]
		[DataRow("/passive /norestart LAUNCHAPP=1", DisplayName = "published release arguments")]
		[DataRow("/QN  /NoRestart\tlaunchapp=0", DisplayName = "case, repeated spaces and tabs")]
		[DataRow("/quiet /qb", DisplayName = "other UI levels")]
		[DataRow("", DisplayName = "empty")]
		[DataRow("   ", DisplayName = "blank")]
		[DataRow((string)null, DisplayName = "none")]
		public void AreAllowedInstallerArguments_AllowedArguments_AreAccepted(string arguments)
		{
			Assert.IsTrue(PackageIntegrity.AreAllowedInstallerArguments(arguments, out var rejectedArgument));
			Assert.IsNull(rejectedArgument);
		}

		[DataTestMethod]
		[DataRow(@"/passive TRANSFORMS=\\attacker\share\x.mst", @"TRANSFORMS=\\attacker\share\x.mst", DisplayName = "foreign transform")]
		[DataRow(@"/l*v C:\Windows\System32\evil.dll", "/l*v", DisplayName = "elevated log file")]
		[DataRow("/passive REBOOT=Force", "REBOOT=Force", DisplayName = "unknown property")]
		[DataRow("/x {00000000-0000-0000-0000-000000000000}", "/x", DisplayName = "uninstall switch")]
		[DataRow("\"/passive\"", "\"/passive\"", DisplayName = "quoted argument")]
		[DataRow("/passive\r\nTRANSFORMS=x.mst", "/passive\r\nTRANSFORMS=x.mst", DisplayName = "line break")]
		[DataRow("LAUNCHAPP=1\u00A0/passive", "LAUNCHAPP=1\u00A0/passive", DisplayName = "no-break space")]
		public void AreAllowedInstallerArguments_AnythingElse_IsRejected(string arguments, string expectedRejected)
		{
			Assert.IsFalse(PackageIntegrity.AreAllowedInstallerArguments(arguments, out var rejectedArgument));
			Assert.AreEqual(expectedRejected, rejectedArgument);
		}

		[TestMethod]
		public void AreAllowedInstallerArguments_LongRejectedArgument_IsShortenedForMessages()
		{
			var arguments = "TRANSFORMS=" + new string('x', 500);

			Assert.IsFalse(PackageIntegrity.AreAllowedInstallerArguments(arguments, out var rejectedArgument));
			Assert.AreEqual(arguments.Substring(0, 64) + "...", rejectedArgument);
		}
	}
}
