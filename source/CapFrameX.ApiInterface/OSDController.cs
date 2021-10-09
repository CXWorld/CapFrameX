using CapFrameX.Contracts.Overlay;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Globalization;
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
                double entryValue = 0;

                if (entry.Value != null)
                {
                    try 
                    {
                        entryValue = Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture);
                    }
                    catch { entryValue = double.NaN; }
                }

                return entry.IsNumeric ? string.Format(CultureInfo.InvariantCulture, entry.ValueAlignmentAndDigits, entryValue) + (entry.Identifier.Contains("temperature") ? "°C " : entry.ValueUnitFormat ) : entry.Value?.ToString();
            } catch(Exception)
            {
                return string.Empty;
            }
        }
    }
}
