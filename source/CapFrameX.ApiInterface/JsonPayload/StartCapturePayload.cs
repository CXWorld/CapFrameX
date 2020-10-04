using CapFrameX.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Remote.JsonPayload
{
    public class StartCapturePayload
    {
        public double CaptureTime { get; set; }
        public string ProcessName { get; set; }
        public bool UseRunHistory { get; set; }
        public bool UseAggregation { get; set; }
        public bool SaveAggregationOnly { get; set; }
        public string CaptureFileMode { get; set; } = Enum.GetName(typeof(ECaptureFileMode), ECaptureFileMode.Json);
    }
}
