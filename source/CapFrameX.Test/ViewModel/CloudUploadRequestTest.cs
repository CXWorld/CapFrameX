using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class CloudUploadRequestTest
    {
        [TestMethod]
        public void GetUploadRequestUri_EscapesTheDescription()
        {
            Assert.AreEqual("SessionCollections?description=RTX%204090%20%26%20DLSS%20%23tested%201%2B1",
                CloudViewModel.GetUploadRequestUri("RTX 4090 & DLSS #tested 1+1"));
        }

        [TestMethod]
        public void GetUploadRequestUri_DescriptionRoundTrips()
        {
            const string description = "Cyberpunk 2077 – 4K/Ultra & RT? 100%";
            var uri = new Uri(new Uri("https://example.com/api/"), CloudViewModel.GetUploadRequestUri(description));

            Assert.AreEqual(description, Uri.UnescapeDataString(uri.Query.Substring("?description=".Length)));
        }

        [TestMethod]
        public void GetUploadRequestUri_NoDescription_SendsAnEmptyParameter()
        {
            Assert.AreEqual("SessionCollections?description=", CloudViewModel.GetUploadRequestUri(null));
            Assert.AreEqual("SessionCollections?description=", CloudViewModel.GetUploadRequestUri(string.Empty));
        }
    }
}
