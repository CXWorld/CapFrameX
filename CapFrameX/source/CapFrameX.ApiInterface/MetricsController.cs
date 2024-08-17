using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Globalization;
using System.Linq;

namespace CapFrameX.ApiInterface
{
    public class MetricsController: WebApiController
    {
        private readonly IOnlineMetricService _metricService;

        public MetricsController(IOnlineMetricService metricService)
        {
            _metricService = metricService;
        }
        
        [Route(HttpVerbs.Get, "/metrics")]
        public double[] GetOsd([QueryField] string metricNames)
        {
            try
            {
                return metricNames
                    .Split(',')
                    .Select(metricName => (EMetric)Enum.Parse(typeof(EMetric), metricName))
                    .Select(metric => _metricService.GetOnlineFpsMetricValue(metric))
                    .ToArray();

                // example: http://localhost:1337/api/metrics?metricNames=P95,Average,P5
            }
            catch (ArgumentException)
            {
                Response.StatusCode = 400;
                return Array.Empty<double>();
            }
        }
    }
}
