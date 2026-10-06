using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Checks that stand between a manifest and code running on the user's machine: the package
	/// must be something the shell may execute, its content must be what the manifest promised, and
	/// it may only be started with arguments the release policy uses. <see cref="PackageSignature"/>
	/// adds who built it.
	/// </summary>
	internal static class PackageIntegrity
	{
		/// <summary>
		/// The only installer arguments a package may be started with. They come from the catalog,
		/// wait in the user-writable pending-update marker, and the installer runs elevated: with
		/// TRANSFORMS= or /l*v even a genuinely signed MSI would run foreign code or write files as
		/// administrator. A new argument has to ship here in a release before the catalog uses it.
		/// </summary>
		public static readonly IReadOnlyCollection<string> AllowedInstallerArguments = new[]
		{
			"/passive", "/quiet", "/qn", "/qb", "/norestart", "LAUNCHAPP=0", "LAUNCHAPP=1"
		};

		private const int MaximumReportedArgumentLength = 64;

		/// <summary>
		/// True when every argument is on <see cref="AllowedInstallerArguments"/>; none at all is fine.
		/// Arguments are split at spaces and tabs only, so quotes, line breaks or other whitespace stay
		/// inside a token and reject it rather than letting msiexec split the line differently.
		/// </summary>
		/// <param name="rejectedArgument">The first argument that is not allowed, shortened for messages.</param>
		public static bool AreAllowedInstallerArguments(string arguments, out string rejectedArgument)
		{
			rejectedArgument = (arguments ?? string.Empty)
				.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.FirstOrDefault(argument => !AllowedInstallerArguments.Contains(argument, StringComparer.OrdinalIgnoreCase));

			if (rejectedArgument?.Length > MaximumReportedArgumentLength)
				rejectedArgument = rejectedArgument.Substring(0, MaximumReportedArgumentLength) + "...";

			return rejectedArgument == null;
		}

		/// <summary>
		/// The downloaded file is handed to the shell, so the manifest must not be able to place
		/// arbitrary file types into the updates folder and have them started.
		/// </summary>
		public static bool HasAllowedExtension(string fileName)
		{
			var extension = Path.GetExtension(fileName ?? string.Empty);

			return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase);
		}

		public static string ComputeSha256(string filePath)
		{
			using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return ComputeSha256(stream);
			}
		}

		public static string ComputeSha256(Stream stream)
		{
			using (var sha256 = SHA256.Create())
			{
				return ToHex(sha256.ComputeHash(stream));
			}
		}

		public static string ToHex(byte[] hash)
			=> BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();

		public static bool HashMatches(string expected, string actual)
			=> !string.IsNullOrWhiteSpace(expected)
				&& string.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase);
	}
}
