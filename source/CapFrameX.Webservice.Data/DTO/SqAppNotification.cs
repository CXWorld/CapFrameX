using Newtonsoft.Json;
using Squidex.ClientLibrary;
using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.DTO
{
    public class SqAppNotificationDataDTO
    {
        public bool IsActive { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SqAppNotificationData
    {
        [JsonConverter(typeof(InvariantConverter))]
        public bool IsActive { get; set; }
        [JsonConverter(typeof(InvariantConverter))]
        public string Message { get; set; }
        [JsonConverter(typeof(InvariantConverter))]
        public DateTime Timestamp { get; set; }
    }

    public class SqAppNotification : Content<SqAppNotificationData>
    {

    }
}
