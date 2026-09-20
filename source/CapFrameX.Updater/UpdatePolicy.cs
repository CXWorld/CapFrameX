using System;

namespace CapFrameX.Updater
{
	/// <summary>Shared version rules for catalog selection, staging and startup installation.</summary>
	internal static class UpdatePolicy
	{
		public static readonly Version MinimumRollbackVersion = new Version(1, 9, 0, 0);

		public static Version Normalize(Version version)
			=> version == null
				? new Version(0, 0, 0, 0)
				: new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

		public static string Format(Version version)
			=> Normalize(version).ToString(4);

		public static bool IsBelowRollbackFloor(Version version)
			=> Normalize(version) < MinimumRollbackVersion;

		public static bool IsSameVersion(Version left, Version right)
			=> Normalize(left) == Normalize(right);

		public static bool IsDowngrade(Version target, Version current)
			=> Normalize(target) < Normalize(current);
	}
}
