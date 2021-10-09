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
            
            return Task.FromResult(GetEntries(_overlayService, showAll));
        }


        public static string[] GetEntries(IOverlayService overlayService, bool showAll)
        {
            return overlayService.CurrentOverlayEntries
                .Where(e => showAll || e.ShowOnOverlay)
                .GroupBy(e => e.GroupName)
                .Select(g => $"{g.Key}: {string.Join(" ", g.Select(FormatEntry))}")
                .ToArray();
        }

        private static string FormatEntry(IOverlayEntry entry)
        {
            try
            {
                double entryValue = double.NaN;

                if (entry.Value != null)
                {
                    double.TryParse(entry.Value.ToString(), out entryValue);
                }

                return entry.IsNumeric ? string.Format(entry.ValueAlignmentAndDigits, (decimal)entryValue) + entry.ValueUnitFormat.Trim() : entry.Value?.ToString();
            } catch(Exception)
            {
                return string.Empty;
            }
        }
    }
}
