using System;
using System.IO;
using System.Security.Cryptography;

namespace CapFrameX.Updater
{
	/// <summary>
	/// The two checks that stand between a manifest and code running on the user's machine: the
	/// package must be something the shell may execute, and its content must be what the manifest
	/// promised.
	/// </summary>
	internal static class PackageIntegrity
	{
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
			using (var sha256 = SHA256.Create())
			using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
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
