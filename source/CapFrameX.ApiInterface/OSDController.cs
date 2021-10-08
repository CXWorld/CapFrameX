using CapFrameX.Contracts.Overlay;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.ApiInterface
{
    public class OSDController: WebApiController
    {
        private readonly IOverlayEntryProvider _overlayEntryProvider;
        private readonly IOverlayEntryCore _overlayEntryCore;

        public OSDController(IOverlayEntryProvider overlayEntryProvider, IOverlayEntryCore overlayEntryCore)
        {
            _overlayEntryProvider = overlayEntryProvider;
            _overlayEntryCore = overlayEntryCore;
        }

        [Route(HttpVerbs.Get, "/osd")]
        public Task<string[]> GetOsd([QueryField] bool showAll)
        {
            return Task.FromResult(_overlayEntryCore.OverlayEntryDict.Values.Where(e => showAll || e.ShowOnOverlay).Select(e => $"{e.GroupName}: {e.Value}{e.ValueUnitFormat}".Trim()).ToArray());
        }
    }
}
