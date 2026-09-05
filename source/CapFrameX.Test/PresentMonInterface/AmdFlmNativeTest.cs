using System;
using System.Runtime.InteropServices;
using CapFrameX.Contracts.Latency;
using CapFrameX.PresentMonInterface.AmdFlm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class AmdFlmNativeTest
    {
        [TestMethod]
        public void Config_UsesPassiveClicksAndVulkanCompatibleCapture()
        {
            var config = AmdFlmNative.Config.Create(new AmdFlmSettings(1, 0, .4, .45, .2, .25, 3));
            Assert.AreEqual(1, config.MouseEventType);
            Assert.AreEqual(1, config.InitAmfUsingDx12);
            Assert.AreEqual(1, config.Codec);
            Assert.AreEqual(1, config.CaptureOutputIndex);
            Assert.AreEqual(56, Marshal.SizeOf<AmdFlmNative.Config>());
            Assert.AreEqual(48, Marshal.SizeOf<AmdFlmNative.Diagnostics>());
        }

        [TestMethod]
        public void NativeRuntime_IsPackagedAndRejectsSyntheticInputBeforeInitialization()
        {
            // Exercises the real packaged C ABI without requiring a GPU or starting capture.
            var config = AmdFlmNative.Config.Create(new AmdFlmSettings(0, 0, .4, .45, .2, .25, 3));
            config.MouseEventType = 0;
            Assert.AreEqual(-1, AmdFlmNative.FlmCreate(ref config, out IntPtr handle));
            Assert.AreEqual(IntPtr.Zero, handle);
            StringAssert.Contains(AmdFlmNative.GetLastError(), "passive click mode required");
        }

        [TestMethod]
        public void NativeRuntime_RejectsInvalidRegionAndIncompatibleConfig()
        {
            var config = AmdFlmNative.Config.Create(new AmdFlmSettings(0, 0, .4, .45, .2, .25, 3));
            config.CaptureWidth = .9f;
            Assert.AreEqual(-1, AmdFlmNative.FlmCreate(ref config, out IntPtr handle));
            Assert.AreEqual(IntPtr.Zero, handle);
            config.CaptureWidth = float.NaN;
            Assert.AreEqual(-1, AmdFlmNative.FlmCreate(ref config, out handle));
            config.StructSize = 52; // Earlier interop layout lacked the output index.
            Assert.AreEqual(-1, AmdFlmNative.FlmCreate(ref config, out handle));
        }

        [TestMethod]
        public void NativeRuntime_ExportsDiagnostics()
        {
            IntPtr library = NativeLibrary.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "CapFrameX.FLM.dll"));
            try
            {
                foreach (string name in new[] { "FlmCreate", "FlmStart", "FlmStop", "FlmTryGetSample", "FlmGetDiagnostics", "FlmGetLastError", "FlmDestroy" })
                    Assert.AreNotEqual(IntPtr.Zero, NativeLibrary.GetExport(library, name), name);
            }
            finally
            {
                NativeLibrary.Free(library);
            }
        }

        [TestMethod]
        public void Diagnostics_DistinguishesNoFramesFromNoResponseAndWaitingForClick()
        {
            var diagnostics = AmdFlmNative.Diagnostics.Create();
            diagnostics.State = 6;
            Assert.AreEqual(AmdFlmState.NoFrames, diagnostics.ToStatus().State);
            diagnostics.State = 4;
            diagnostics.Timeouts = 2;
            Assert.AreEqual(AmdFlmState.NoResponse, diagnostics.ToStatus().State);
            Assert.AreEqual(2ul, diagnostics.ToStatus().Timeouts);
            diagnostics.State = 1;
            Assert.AreEqual(AmdFlmState.WaitingForClick, diagnostics.ToStatus().State);
        }
    }
}
