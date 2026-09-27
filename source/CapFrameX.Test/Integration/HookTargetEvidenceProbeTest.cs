using System.Collections.Generic;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookTargetEvidenceProbeTest
    {
        private const string SystemRoot = @"C:\Windows";

        [TestMethod]
        public void FromModules_ClassifiesTheResonanceConstellation()
        {
            // Streamline DLSS-G plus OptiScaler: a dxgi.dll proxy next to the game and two
            // resident FidelityFX loader copies (game root and OptiScaler folder).
            var modules = new List<HookModuleRecord>
            {
                new HookModuleRecord("Resonance.exe", @"E:\Games\Resonance\Resonance.exe"),
                new HookModuleRecord("dxgi.dll", @"E:\Games\Resonance\dxgi.dll"),
                new HookModuleRecord("dxgi.dll", @"C:\Windows\System32\dxgi.dll"),
                new HookModuleRecord("d3d12.dll", @"C:\Windows\System32\d3d12.dll"),
                new HookModuleRecord("sl.interposer.dll", @"E:\Games\Resonance\sl.interposer.dll"),
                new HookModuleRecord("sl.dlss_g.dll", @"E:\Games\Resonance\sl.dlss_g.dll"),
                new HookModuleRecord("amd_fidelityfx_loader_dx12.dll", @"E:\Games\Resonance\amd_fidelityfx_loader_dx12.dll"),
                new HookModuleRecord("amd_fidelityfx_loader_dx12.dll", @"E:\Games\Resonance\OptiScaler\amd_fidelityfx_loader_dx12.dll"),
                new HookModuleRecord("amd_fidelityfx_framegeneration_dx12.dll", @"E:\Games\Resonance\OptiScaler\amd_fidelityfx_framegeneration_dx12.dll"),
                new HookModuleRecord("libxess_fg.dll", @"E:\Games\Resonance\OptiScaler\libxess_fg.dll")
            };

            HookTargetEvidence evidence = HookTargetEvidence.FromModules(modules, "DXGI",
                HookAttachMode.Late, SystemRoot);

            Assert.IsTrue(evidence.IsKnown);
            Assert.IsTrue(evidence.Streamline);
            Assert.IsTrue(evidence.StreamlineDlssg);
            Assert.IsTrue(evidence.XeFg);
            Assert.IsTrue(evidence.FfxFg);
            Assert.AreEqual(2, evidence.FfxLoaderCopies);
            Assert.IsTrue(evidence.NonSystemDxgiProxy);
            Assert.IsTrue(evidence.D3D12Loaded);
            Assert.IsTrue(evidence.SuggestsFidelityFxArmingHazard);
            Assert.AreEqual("dxgiproxy+ffxfg+ffxloader2+sl+sldlssg+xefg", evidence.Signature);
        }

        [TestMethod]
        public void FromModules_IgnoresTheSystemDxgiAndASingleLoader()
        {
            var modules = new List<HookModuleRecord>
            {
                new HookModuleRecord("dxgi.dll", @"c:\windows\SYSTEM32\dxgi.dll"),
                new HookModuleRecord("d3d12.dll", @"C:\Windows\System32\d3d12.dll"),
                new HookModuleRecord("amd_fidelityfx_loader_dx12.dll", @"E:\Games\Foo\amd_fidelityfx_loader_dx12.dll")
            };

            HookTargetEvidence evidence = HookTargetEvidence.FromModules(modules, "DXGI",
                HookAttachMode.Late, SystemRoot);

            Assert.IsFalse(evidence.NonSystemDxgiProxy);
            Assert.AreEqual(1, evidence.FfxLoaderCopies);
            Assert.IsFalse(evidence.SuggestsFidelityFxArmingHazard);
            Assert.IsTrue(evidence.HasFidelityFxEvidence);
            Assert.IsFalse(evidence.HasFrameGenerationRuntime);
            Assert.AreEqual(HookTargetEvidence.EmptySignature, evidence.Signature);
        }

        [TestMethod]
        public void FromModules_TreatsTheWow64DirectoryAsSystem()
        {
            var modules = new List<HookModuleRecord>
            {
                new HookModuleRecord("dxgi.dll", @"C:\Windows\SysWOW64\dxgi.dll")
            };

            Assert.IsFalse(HookTargetEvidence.FromModules(modules, "DXGI", HookAttachMode.Late,
                SystemRoot).NonSystemDxgiProxy);
        }

        [TestMethod]
        public void Signature_IsIndependentOfModuleOrderAndPaths()
        {
            var first = new List<HookModuleRecord>
            {
                new HookModuleRecord("libxess_fg.dll", @"D:\A\libxess_fg.dll"),
                new HookModuleRecord("sl.interposer.dll", @"D:\A\sl.interposer.dll")
            };
            var second = new List<HookModuleRecord>
            {
                new HookModuleRecord("SL.INTERPOSER.DLL", @"E:\B\sl.interposer.dll"),
                new HookModuleRecord("libxess_fg.dll", @"E:\B\x\libxess_fg.dll")
            };

            Assert.AreEqual(
                HookTargetEvidence.FromModules(first, null, HookAttachMode.Early, SystemRoot).Signature,
                HookTargetEvidence.FromModules(second, "DXGI", HookAttachMode.Late, SystemRoot).Signature);
            Assert.AreEqual("sl+xefg",
                HookTargetEvidence.FromModules(first, null, HookAttachMode.Early, SystemRoot).Signature);
        }

        [TestMethod]
        public void Unknown_NeverClaimsAbsenceAndHasItsOwnSignature()
        {
            HookTargetEvidence unknown = HookTargetEvidence.Unknown("DXGI", HookAttachMode.Late);

            Assert.IsFalse(unknown.IsKnown);
            Assert.IsFalse(unknown.HasFrameGenerationRuntime);
            Assert.AreEqual(HookTargetEvidence.UnknownSignature, unknown.Signature);
            Assert.IsNull(HookCompatibilityStagePlanner.ResolveEarlyInjectionGateModule(unknown));
        }

        [TestMethod]
        public void Probe_ReadsTheTestHostItself()
        {
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

            HookTargetEvidence evidence = HookTargetEvidenceProbe.Probe(pid, "DXGI",
                HookAttachMode.Late);

            Assert.IsTrue(evidence.IsKnown);
            Assert.IsFalse(evidence.HasFrameGenerationRuntime);
            Assert.IsFalse(evidence.NonSystemDxgiProxy);
        }
    }
}
