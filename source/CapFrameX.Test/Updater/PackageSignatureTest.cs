using CapFrameX.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CapFrameX.Test.Updater
{
	/// <summary>
	/// The Verify tests run the real Authenticode check against the PresentMon build the repository
	/// ships, which carries the same Certum signature as the release installers. The Evaluate tests
	/// cover the policy with generated certificates, including cases no real file can produce.
	/// </summary>
	[TestClass]
	public class PackageSignatureTest
	{
		private const string PublisherSubject = "CN=Open Source Developer Mark Daniel Fangmeyer, O=Open Source Developer, C=DE";
		private const string CertumIssuer = "CN=Certum Code Signing 2021 CA, O=Asseco Data Systems S.A., C=PL";
		private const int BadDigest = unchecked((int)0x80096010);
		private const int NoSignature = unchecked((int)0x800B0100);
		private static readonly string[] NoPins = new string[0];

		private string _folder;

		[TestInitialize]
		public void Setup()
		{
			_folder = Path.Combine(Path.GetTempPath(), "CapFrameXSignatureTest_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{
				if (Directory.Exists(_folder))
					Directory.Delete(_folder, true);
			}
			catch (IOException)
			{
				// A leftover temp folder must not fail the test run.
			}
		}

		[TestMethod]
		public void Verify_PublisherSignedBinary_IsTrusted()
		{
			var result = PackageSignature.Verify(SignedBinaryPath());

			Assert.IsTrue(result.IsTrusted, result.Reason);
			StringAssert.Contains(result.Signer, "Mark Daniel Fangmeyer");
		}

		[TestMethod]
		public void Verify_PartialDownloadName_IsTrusted()
		{
			// UpdateService checks the download before it gets its installer name, so the file type
			// must come from the content, not from the extension.
			var partial = Path.Combine(_folder, "CapFrameXInstaller.msi.part");
			File.Copy(SignedBinaryPath(), partial);

			var result = PackageSignature.Verify(partial);

			Assert.IsTrue(result.IsTrusted, result.Reason);
		}

		[TestMethod]
		public void Verify_TamperedBinary_IsRejected()
		{
			var bytes = File.ReadAllBytes(SignedBinaryPath());
			bytes[bytes.Length / 2] ^= 0xFF;
			var tampered = Path.Combine(_folder, "CapFrameXInstaller.exe");
			File.WriteAllBytes(tampered, bytes);

			var result = PackageSignature.Verify(tampered);

			Assert.IsFalse(result.IsTrusted);
			StringAssert.Contains(result.Reason, "TRUST_E_BAD_DIGEST");
		}

		[TestMethod]
		public void Verify_UnsignedFile_IsRejected()
		{
			var unsigned = Path.Combine(_folder, "CapFrameXInstaller.msi");
			File.WriteAllText(unsigned, "not a real installer");

			var result = PackageSignature.Verify(unsigned);

			Assert.IsFalse(result.IsTrusted);
			Assert.IsNull(result.Signer);
		}

		[TestMethod]
		public void Verify_ForeignPublisher_IsRejected()
		{
			// The .NET runtime carries Microsoft's Authenticode signature: valid, but someone else's.
			var result = PackageSignature.Verify(typeof(object).Assembly.Location);

			if (result.Signer == null)
				Assert.Inconclusive("This .NET runtime build carries no embedded signature.");

			Assert.IsFalse(result.IsTrusted);
			StringAssert.Contains(result.Reason, "not by the CapFrameX publisher");
		}

		[TestMethod]
		public void Evaluate_PublisherCertificateFromCertum_IsTrusted()
		{
			using (var signer = IssueCertificate(PublisherSubject, CertumIssuer))
			{
				var result = PackageSignature.Evaluate(PackageSignature.TrustSuccess, signer, NoPins);

				Assert.IsTrue(result.IsTrusted, result.Reason);
			}
		}

		[TestMethod]
		public void Evaluate_PublisherNameFromAnotherCa_IsRejected()
		{
			using (var signer = IssueCertificate(PublisherSubject, "CN=Other Code Signing CA, O=Other Trust Services Ltd., C=US"))
			{
				Assert.IsFalse(PackageSignature.Evaluate(PackageSignature.TrustSuccess, signer, NoPins).IsTrusted);
			}
		}

		[TestMethod]
		public void Evaluate_OtherPublisherFromCertum_IsRejected()
		{
			using (var signer = IssueCertificate("CN=Open Source Developer Someone Else, O=Open Source Developer, C=DE", CertumIssuer))
			{
				Assert.IsFalse(PackageSignature.Evaluate(PackageSignature.TrustSuccess, signer, NoPins).IsTrusted);
			}
		}

		[TestMethod]
		public void Evaluate_PublisherCertificateWithoutTrustedChain_IsRejected()
		{
			// A look-alike "Certum" CA that Windows does not trust ends in an untrusted root.
			using (var signer = IssueCertificate(PublisherSubject, CertumIssuer))
			{
				Assert.IsFalse(PackageSignature.Evaluate(PackageSignature.CertUntrustedRoot, signer, NoPins).IsTrusted);
			}
		}

		[TestMethod]
		public void Evaluate_PinnedSelfSignedCertificate_IsTrustedDespiteUntrustedRoot()
		{
			using (var signer = SelfSigned("CN=CapFrameX Update Self-Signed Publisher"))
			{
				var result = PackageSignature.Evaluate(PackageSignature.CertUntrustedRoot, signer, new[] { signer.Thumbprint });

				Assert.IsTrue(result.IsTrusted, result.Reason);
			}
		}

		[TestMethod]
		public void Evaluate_UnpinnedSelfSignedCertificate_IsRejected()
		{
			// Same name as the release certificate, different key: only the thumbprint counts.
			using (var signer = SelfSigned("CN=CapFrameX Update Self-Signed Publisher"))
			{
				Assert.IsFalse(PackageSignature.Evaluate(PackageSignature.CertUntrustedRoot, signer,
					PackageSignature.PinnedSelfSignedThumbprints).IsTrusted);
			}
		}

		[TestMethod]
		public void Evaluate_PinnedSelfSignedCertificateWithBrokenDigest_IsRejected()
		{
			using (var signer = SelfSigned("CN=CapFrameX Update Self-Signed Publisher"))
			{
				Assert.IsFalse(PackageSignature.Evaluate(BadDigest, signer, new[] { signer.Thumbprint }).IsTrusted);
			}
		}

		[TestMethod]
		public void Evaluate_NoSigner_IsRejected()
		{
			var result = PackageSignature.Evaluate(NoSignature, null, NoPins);

			Assert.IsFalse(result.IsTrusted);
			StringAssert.Contains(result.Reason, "TRUST_E_NOSIGNATURE");
		}

		private static string SignedBinaryPath()
		{
			var directory = new DirectoryInfo(AppContext.BaseDirectory);
			while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CapFrameX.sln")))
				directory = directory.Parent;

			Assert.IsNotNull(directory, "The repository root was not found.");
			var presentMon = Directory.GetFiles(
				Path.Combine(directory.FullName, "source", "CapFrameX.PresentMonInterface", "PresentMon"), "CX-PresentMon-*.exe")
				.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
				.LastOrDefault();

			Assert.IsNotNull(presentMon, "The signed PresentMon build is missing from the repository.");
			return presentMon;
		}

		private static X509Certificate2 IssueCertificate(string subject, string issuer)
		{
			using (var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256))
			using (var signerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256))
			{
				var issuerRequest = new CertificateRequest(issuer, issuerKey, HashAlgorithmName.SHA256);
				issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));

				using (var issuerCertificate = issuerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)))
				{
					var signerRequest = new CertificateRequest(subject, signerKey, HashAlgorithmName.SHA256);
					return signerRequest.Create(issuerCertificate, DateTimeOffset.UtcNow.AddHours(-1),
						DateTimeOffset.UtcNow.AddMonths(6), new byte[] { 1, 2, 3, 4 });
				}
			}
		}

		private static X509Certificate2 SelfSigned(string subject)
		{
			using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256))
			{
				return new CertificateRequest(subject, key, HashAlgorithmName.SHA256)
					.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
			}
		}
	}
}
