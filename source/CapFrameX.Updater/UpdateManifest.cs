using Newtonsoft.Json;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Wire format served by the update server. See <c>update-manifest.sample.json</c> next to this
	/// file for a complete example.
	/// </summary>
	public class UpdateManifest
	{
		/// <summary>Version the package installs, e.g. "1.9.1". Required.</summary>
		[JsonProperty("version")]
		public string Version { get; set; }

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
		/// Hex encoded SHA-256 of the package. Strongly recommended: when present the download is
		/// rejected unless it matches, and the staged file is re-verified before it is executed.
		/// </summary>
		[JsonProperty("sha256")]
		public string Sha256 { get; set; }

		/// <summary>Package size in bytes. Used for the progress display only. Optional.</summary>
		[JsonProperty("size")]
		public long Size { get; set; }

		/// <summary>Command line handed to the installer, e.g. "/passive /norestart". Optional.</summary>
		[JsonProperty("arguments")]
		public string Arguments { get; set; }
	}
}
