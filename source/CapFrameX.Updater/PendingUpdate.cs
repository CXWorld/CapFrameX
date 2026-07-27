using Newtonsoft.Json;
using System;
using System.IO;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Marker written next to a downloaded package. Its presence is the only thing that makes the
	/// next app start hand the package to the installer, so it is written last (after the download
	/// has been verified) and deleted first (before the installer is launched).
	/// </summary>
	public class PendingUpdate
	{
		public const string FileName = "pending-update.json";

		/// <summary>Version the staged package installs.</summary>
		[JsonProperty("version")]
		public string Version { get; set; }

		/// <summary>File name of the package, relative to the updates folder.</summary>
		[JsonProperty("packageFile")]
		public string PackageFile { get; set; }

		/// <summary>Hex encoded SHA-256 the package is re-verified against before it is executed.</summary>
		[JsonProperty("sha256")]
		public string Sha256 { get; set; }

		/// <summary>Command line handed to the installer.</summary>
		[JsonProperty("arguments")]
		public string Arguments { get; set; }

		[JsonProperty("stagedUtc")]
		public DateTime StagedUtc { get; set; }

		public static string GetMarkerPath(string updatesFolder)
			=> Path.Combine(updatesFolder, FileName);

		/// <summary>Returns the staged update, or null when nothing is pending or the marker is unreadable.</summary>
		public static PendingUpdate Load(string updatesFolder)
		{
			var markerPath = GetMarkerPath(updatesFolder);

			if (!File.Exists(markerPath))
				return null;

			return JsonConvert.DeserializeObject<PendingUpdate>(File.ReadAllText(markerPath));
		}

		public void Save(string updatesFolder)
		{
			Directory.CreateDirectory(updatesFolder);
			File.WriteAllText(GetMarkerPath(updatesFolder),
				JsonConvert.SerializeObject(this, Formatting.Indented));
		}

		public static void Clear(string updatesFolder)
		{
			var markerPath = GetMarkerPath(updatesFolder);

			if (File.Exists(markerPath))
				File.Delete(markerPath);
		}
	}
}
