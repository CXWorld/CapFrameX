using System;

namespace CapFrameX.Contracts.Update
{
	/// <summary>
	/// The part of the server manifest the app acts on: what the update contains and where to get
	/// it. Kept separate from the wire format so the server payload can evolve independently.
	/// </summary>
	public class UpdatePackageInfo
	{
		/// <summary>Version the package installs. Compared against the running assembly version.</summary>
		public Version Version { get; set; }

		/// <summary>Whether this package is a regular release or a beta build.</summary>
		public EUpdateChannel Channel { get; set; }

		/// <summary>Release date, if the manifest supplies one.</summary>
		public DateTime? ReleaseDate { get; set; }

		/// <summary>One or two sentences shown in the update dialog.</summary>
		public string Summary { get; set; }

		/// <summary>Short bullet points shown below the summary. May be null or empty.</summary>
		public string[] Highlights { get; set; }

		/// <summary>Absolute URI of the installer package (.exe or .msi).</summary>
		public Uri PackageUri { get; set; }

		/// <summary>Required hex encoded SHA-256 of the package, verified after download and before execution.</summary>
		public string Sha256 { get; set; }

		/// <summary>Package size in bytes, used for the progress display. Zero when unknown.</summary>
		public long SizeInBytes { get; set; }

		/// <summary>Command line handed to the installer, e.g. "/passive /norestart".</summary>
		public string InstallerArguments { get; set; }

		/// <summary>Optional link to the full release notes, opened in the browser.</summary>
		public Uri ReleaseNotesUri { get; set; }
	}
}
