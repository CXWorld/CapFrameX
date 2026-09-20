using Newtonsoft.Json;

namespace CapFrameX.Updater
{
	/// <summary>Version catalog served by the isolated CapFrameX update service.</summary>
	public class UpdateCatalogManifest
	{
		[JsonProperty("schemaVersion")]
		public int SchemaVersion { get; set; }

		[JsonProperty("minimumVersion")]
		public string MinimumVersion { get; set; }

		[JsonProperty("latestRelease")]
		public string LatestRelease { get; set; }

		[JsonProperty("latestBeta")]
		public string LatestBeta { get; set; }

		[JsonProperty("releases")]
		public UpdateManifest[] Releases { get; set; }
	}

	/// <summary>
	/// One release inside the update server's version catalog.
	/// </summary>
	public class UpdateManifest
	{
		/// <summary>Version the package installs, e.g. "1.9.1.4". Required.</summary>
		[JsonProperty("version")]
		public string Version { get; set; }

		/// <summary>Publication channel: "release" or "beta". Required.</summary>
		[JsonProperty("channel")]
		public string Channel { get; set; }

		/// <summary>Release date in a format <see cref="System.DateTime.TryParse(string, out System.DateTime)"/> accepts. Optional.</summary>
		[JsonProperty("releaseDate")]
		public string ReleaseDate { get; set; }

		/// <summary>One or two sentences shown in the update dialog. Optional.</summary>
		[JsonProperty("summary")]
		public string Summary { get; set; }

		/// <summary>Short bullet points shown below the summary. Optional.</summary>
		[JsonProperty("highlights")]
		public string[] Highlights { get; set; }

		/// <summary>Link to the full release notes, opened in the browser. Optional.</summary>
		[JsonProperty("releaseNotesUrl")]
		public string ReleaseNotesUrl { get; set; }

		/// <summary>Describes the installer package. Required.</summary>
		[JsonProperty("package")]
		public UpdateManifestPackage Package { get; set; }
	}

	/// <summary>
	/// The installer package of an <see cref="UpdateManifest"/>.
	/// </summary>
	public class UpdateManifestPackage
	{
		/// <summary>
		/// Absolute URL, or a URL relative to the manifest. Must point at an .exe or .msi -
		/// the app hands the downloaded file straight to the shell.
		/// </summary>
		[JsonProperty("url")]
		public string Url { get; set; }

		/// <summary>
		/// Hex encoded SHA-256 of the package. Required: the release is rejected unless this contains
		/// exactly 64 hexadecimal characters, and the package is re-verified before execution.
		/// </summary>
		[JsonProperty("sha256")]
		public string Sha256 { get; set; }

		/// <summary>Package size in bytes. Required and verified while downloading.</summary>
		[JsonProperty("size")]
		public long Size { get; set; }

		/// <summary>Command line handed to the installer, e.g. "/passive /norestart". Optional.</summary>
		[JsonProperty("arguments")]
		public string Arguments { get; set; }
	}
}
