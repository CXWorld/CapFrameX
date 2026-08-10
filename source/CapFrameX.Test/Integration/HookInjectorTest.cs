using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookInjectorTest
    {
        [TestMethod]
        public void TryGetWow64LoadLibraryRva_ResolvesTheSystemExport()
        {
            bool resolved = HookInjector.TryGetWow64LoadLibraryRva(
                out uint loadLibraryRva, out string error);

            Assert.IsTrue(resolved, error);
            Assert.AreNotEqual(0u, loadLibraryRva);
        }
    }
}
