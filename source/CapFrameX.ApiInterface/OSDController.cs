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
        private readonly IOverlayService _overlayService;

        public OSDController(IOverlayService overlayService)
        {
            _overlayService = overlayService;
        }

        [Route(HttpVerbs.Get, "/osd")]
        public Task<string[]> GetOsd([QueryField] bool showAll)
        {
            
            return Task.FromResult(GetEntries(showAll));
        }

        private string[] GetEntries(bool showAll)
        {
            var entries = _overlayService.CurrentOverlayEntries
                .Where(e => showAll || e.ShowOnOverlay)
                .GroupBy(e => e.GroupName)
                .Select(g => $"{g.Key}: {string.Join(" ", g.Select(e => $"{ e.Value,2:0.00}{e.ValueUnitFormat}".Trim()))}");
            return entries.ToArray();
        }
    }
}
