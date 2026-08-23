using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard.Contracts;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
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
                    .Select(GetMetricValue)
                    .ToArray();

                // example: http://localhost:1337/api/metrics?metricNames=P95,Average,GPUBusy,GPUBusyDeviation,CPUBusy
            }
            catch (ArgumentException)
            {
                Response.StatusCode = 400;
                return Array.Empty<double>();
            }
        }

        private double GetMetricValue(string metricName)
        {
            var normalizedName = metricName.Trim();

            if (normalizedName.Equals("GPUBusy", StringComparison.OrdinalIgnoreCase))
                return _metricService.GetOnlineGpuActiveTimeMetricValue(EMetric.GpuActiveAverage);

            if (normalizedName.Equals("GPUBusyDeviation", StringComparison.OrdinalIgnoreCase))
                return _metricService.GetOnlineGpuActiveTimeDeviationMetricValue();

            if (normalizedName.Equals("CPUBusy", StringComparison.OrdinalIgnoreCase))
                return _metricService.GetOnlineCpuActiveTimeMetricValue(EMetric.CpuActiveAverage);

            var metric = (EMetric)Enum.Parse(typeof(EMetric), normalizedName, ignoreCase: true);

            switch (metric)
            {
                case EMetric.GpuActiveAverage:
                case EMetric.GpuActiveP1:
                case EMetric.GpuActiveOnePercentLowAverage:
                    return _metricService.GetOnlineGpuActiveTimeMetricValue(metric);
                case EMetric.CpuActiveAverage:
                    return _metricService.GetOnlineCpuActiveTimeMetricValue(metric);
                default:
                    return _metricService.GetOnlineFpsMetricValue(metric);
            }
        }
    }
}
