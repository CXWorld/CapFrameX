using CapFrameX.Webservice.Data.Mappings;
using CapFrameX.Webservice.Implementation.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace CapFrameX.Test.Webservice
{
    [TestClass]
    public class WebserviceDependencyTest
    {
        // AutoMapper 15+ is licensed under RPL-1.5 or a commercial license, which does not fit the
        // MIT-licensed CapFrameX. CapFrameX.Webservice.Data also ships with the desktop app.
        [TestMethod]
        [DataRow(typeof(SessionCollectionMappings))]
        [DataRow(typeof(UploadCapturesHandler))]
        public void WebserviceAssemblies_DoNotReferenceAutoMapper(Type typeInAssembly)
        {
            var references = typeInAssembly.Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

            Assert.DoesNotContain("AutoMapper", references);
        }
    }
}
