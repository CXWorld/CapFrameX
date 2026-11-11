using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Outcome of <see cref="PackageSignature.Verify(string)"/>. <see cref="Reason"/> is meant for
	/// the log, in both directions.
	/// </summary>
	internal sealed class PackageSignatureResult
	{
		public PackageSignatureResult(bool isTrusted, string signer, string reason)
		{
			IsTrusted = isTrusted;
			Signer = signer;
			Reason = reason;
		}

		public bool IsTrusted { get; }

		/// <summary>Subject of the signing certificate, or null when the file carries none.</summary>
		public string Signer { get; }

		public string Reason { get; }
	}

	/// <summary>
	/// Authenticode check for update packages. The SHA-256 in the catalog only proves that the
	/// package is the one the update server announced; this check proves that the CapFrameX
	/// publisher built it, so a compromised update server cannot hand out its own installer.
	/// The installer runs elevated, which makes this the check that matters.
	/// </summary>
	/// <remarks>
	/// A package passes with either signature the release policy (UpdateServer PUBLISHING.md)
	/// allows: the Certum code signing certificate of the maintainer, through a chain Windows
	/// trusts, or the pinned self-signed release certificate, whose root Windows cannot trust.
	/// The publisher is pinned by name and issuing CA rather than by thumbprint, so a renewed
	/// Certum certificate keeps working; a certificate with a different name needs a release
	/// signed with the old one that adds the new name first.
	/// Revocation is not checked: it would make the result depend on reaching Certum's CRL
	/// servers, also at the next app start when the staged package is checked again.
	/// </remarks>
	internal static class PackageSignature
	{
		/// <summary>Subject CN of the Certum Open Source code signing certificate (1.9.1.4 and later).</summary>
		internal static readonly IReadOnlyCollection<string> PublisherNames = new[]
		{
			"Open Source Developer Mark Daniel Fangmeyer"
		};

		/// <summary>Organization of every Certum issuing CA.</summary>
		internal const string PublisherIssuerOrganization = "Asseco Data Systems S.A.";

		/// <summary>"CN=CapFrameX Update Self-Signed Publisher", which signed the 1.9.1.0 beta.</summary>
		internal static readonly IReadOnlyCollection<string> PinnedSelfSignedThumbprints = new[]
		{
			"DAA7571197F1199A3ADC359D96DEC2262E088F20"
		};

		internal const int TrustSuccess = 0;
		internal const int CertUntrustedRoot = unchecked((int)0x800B0109);

		private const string CommonNameOid = "2.5.4.3";
		private const string OrganizationOid = "2.5.4.10";

		private static readonly Dictionary<int, string> TrustErrorNames = new Dictionary<int, string>
		{
			[unchecked((int)0x800B0100)] = "TRUST_E_NOSIGNATURE",
			[unchecked((int)0x80096010)] = "TRUST_E_BAD_DIGEST",
			[unchecked((int)0x800B0003)] = "TRUST_E_SUBJECT_FORM_UNKNOWN",
			[unchecked((int)0x800B0004)] = "TRUST_E_SUBJECT_NOT_TRUSTED",
			[unchecked((int)0x800B0111)] = "TRUST_E_EXPLICIT_DISTRUST",
			[unchecked((int)0x800B0101)] = "CERT_E_EXPIRED",
			[unchecked((int)0x800B010A)] = "CERT_E_CHAINING",
			[unchecked((int)0x800B010C)] = "CERT_E_REVOKED",
			[CertUntrustedRoot] = "CERT_E_UNTRUSTEDROOT",
			[unchecked((int)0x80092026)] = "CRYPT_E_SECURITY_SETTINGS"
		};

		public static PackageSignatureResult Verify(string filePath)
		{
			using (var package = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return Verify(package);
			}
		}

		/// <summary>
		/// Verifies the file behind an open stream. Callers that go on to use the file keep the
		/// stream open, which denies writers, so the file cannot be swapped after the check.
		/// </summary>
		public static PackageSignatureResult Verify(FileStream package)
		{
			int trustResult;
			X509Certificate2 signer;

			try
			{
				trustResult = WinVerifyTrust(package.Name, package.SafeFileHandle, out signer);
			}
			catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
			{
				return new PackageSignatureResult(false, null, $"Authenticode verification is unavailable: {ex.Message}");
			}

			using (signer)
			{
				return Evaluate(trustResult, signer, PinnedSelfSignedThumbprints);
			}
		}

		/// <summary>The policy, separated from WinVerifyTrust so it can be tested with any certificate.</summary>
		internal static PackageSignatureResult Evaluate(int trustResult, X509Certificate2 signer,
			IReadOnlyCollection<string> pinnedSelfSignedThumbprints)
		{
			if (signer == null)
				return new PackageSignatureResult(false, null, $"The package carries no usable signature ({Describe(trustResult)}).");

			var subject = signer.Subject;

			if ((trustResult == TrustSuccess || trustResult == CertUntrustedRoot)
				&& pinnedSelfSignedThumbprints.Contains(signer.Thumbprint, StringComparer.OrdinalIgnoreCase)
				&& signer.SubjectName.RawData.AsSpan().SequenceEqual(signer.IssuerName.RawData))
			{
				return new PackageSignatureResult(true, subject, $"Signed with the pinned self-signed release certificate {signer.Thumbprint}.");
			}

			if (trustResult != TrustSuccess)
				return new PackageSignatureResult(false, subject, $"The signature of '{subject}' is not valid ({Describe(trustResult)}).");

			var commonName = GetAttribute(signer.SubjectName, CommonNameOid);
			var issuerOrganization = GetAttribute(signer.IssuerName, OrganizationOid);

			if (commonName == null || !PublisherNames.Contains(commonName, StringComparer.Ordinal)
				|| !string.Equals(issuerOrganization, PublisherIssuerOrganization, StringComparison.Ordinal))
			{
				return new PackageSignatureResult(false, subject,
					$"The package is signed by '{subject}' (issued by '{signer.Issuer}'), not by the CapFrameX publisher.");
			}

			return new PackageSignatureResult(true, subject, $"Signed by '{commonName}' with certificate {signer.Thumbprint} issued by '{signer.Issuer}'.");
		}

		private static string Describe(int trustResult)
			=> TrustErrorNames.TryGetValue(trustResult, out var name)
				? $"{name}, 0x{trustResult:X8}"
				: $"0x{trustResult:X8}";

		private static string GetAttribute(X500DistinguishedName name, string oid)
		{
			foreach (var attribute in name.EnumerateRelativeDistinguishedNames())
			{
				if (!attribute.HasMultipleElements && attribute.GetSingleElementType().Value == oid)
					return attribute.GetSingleElementValue();
			}

			return null;
		}

		/// <summary>
		/// Runs the Authenticode policy (WINTRUST_ACTION_GENERIC_VERIFY_V2) and returns the
		/// certificate that signed the file, read from the same verification state.
		/// </summary>
		private static int WinVerifyTrust(string filePath, SafeFileHandle fileHandle, out X509Certificate2 signer)
		{
			signer = null;
			var addedReference = false;
			var path = IntPtr.Zero;
			var fileInfo = IntPtr.Zero;
			var data = new WinTrustData();
			var action = GenericVerifyV2;

			try
			{
				fileHandle.DangerousAddRef(ref addedReference);
				path = Marshal.StringToHGlobalUni(filePath);
				fileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
				Marshal.StructureToPtr(new WinTrustFileInfo
				{
					cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
					pcwszFilePath = path,
					hFile = fileHandle.DangerousGetHandle()
				}, fileInfo, false);

				data.cbStruct = (uint)Marshal.SizeOf<WinTrustData>();
				data.dwUIChoice = WtdUiNone;
				data.fdwRevocationChecks = WtdRevokeNone;
				data.dwUnionChoice = WtdChoiceFile;
				data.pFile = fileInfo;
				data.dwStateAction = WtdStateActionVerify;
				data.dwProvFlags = WtdRevocationCheckNone;

				var result = NativeWinVerifyTrust(InvalidHandleValue, ref action, ref data);

				var providerData = WTHelperProvDataFromStateData(data.hWVTStateData);
				var primarySigner = providerData == IntPtr.Zero
					? IntPtr.Zero
					: WTHelperGetProvSignerFromChain(providerData, 0, false, 0);

				if (primarySigner != IntPtr.Zero)
				{
					var signerHead = Marshal.PtrToStructure<CryptProviderSignerHead>(primarySigner);
					if (signerHead.csCertChain > 0 && signerHead.pasCertChain != IntPtr.Zero)
					{
						var certificate = Marshal.PtrToStructure<CryptProviderCertHead>(signerHead.pasCertChain).pCert;
						if (certificate != IntPtr.Zero)
							signer = new X509Certificate2(certificate);
					}
				}

				return result;
			}
			finally
			{
				if (data.hWVTStateData != IntPtr.Zero)
				{
					data.dwStateAction = WtdStateActionClose;
					NativeWinVerifyTrust(InvalidHandleValue, ref action, ref data);
				}

				if (fileInfo != IntPtr.Zero)
					Marshal.FreeHGlobal(fileInfo);
				if (path != IntPtr.Zero)
					Marshal.FreeHGlobal(path);
				if (addedReference)
					fileHandle.DangerousRelease();
			}
		}

		private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
		private static readonly Guid GenericVerifyV2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
		private const uint WtdUiNone = 2;
		private const uint WtdRevokeNone = 0;
		private const uint WtdChoiceFile = 1;
		private const uint WtdStateActionVerify = 1;
		private const uint WtdStateActionClose = 2;
		private const uint WtdRevocationCheckNone = 0x10;

		[DllImport("wintrust.dll", EntryPoint = "WinVerifyTrust", ExactSpelling = true)]
		private static extern int NativeWinVerifyTrust(IntPtr hwnd, ref Guid pgActionID, ref WinTrustData pWVTData);

		[DllImport("wintrust.dll", ExactSpelling = true)]
		private static extern IntPtr WTHelperProvDataFromStateData(IntPtr hStateData);

		[DllImport("wintrust.dll", ExactSpelling = true)]
		private static extern IntPtr WTHelperGetProvSignerFromChain(IntPtr pProvData, uint idxSigner,
			[MarshalAs(UnmanagedType.Bool)] bool fCounterSigner, uint idxCounterSigner);

		[StructLayout(LayoutKind.Sequential)]
		private struct WinTrustFileInfo
		{
			public uint cbStruct;
			public IntPtr pcwszFilePath;
			public IntPtr hFile;
			public IntPtr pgKnownSubject;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct WinTrustData
		{
			public uint cbStruct;
			public IntPtr pPolicyCallbackData;
			public IntPtr pSIPClientData;
			public uint dwUIChoice;
			public uint fdwRevocationChecks;
			public uint dwUnionChoice;
			public IntPtr pFile;
			public uint dwStateAction;
			public IntPtr hWVTStateData;
			public IntPtr pwszURLReference;
			public uint dwProvFlags;
			public uint dwUIContext;
			public IntPtr pSignatureSettings;
		}

		/// <summary>Leading fields of CRYPT_PROVIDER_SGNR; FILETIME is spelled out as two DWORDs.</summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct CryptProviderSignerHead
		{
			public uint cbStruct;
			public uint sftVerifyAsOfLow;
			public uint sftVerifyAsOfHigh;
			public uint csCertChain;
			public IntPtr pasCertChain;
		}

		/// <summary>Leading fields of CRYPT_PROVIDER_CERT; element 0 of the chain is the signer.</summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct CryptProviderCertHead
		{
			public uint cbStruct;
			public IntPtr pCert;
		}
	}
}
