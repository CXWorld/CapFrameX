using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Remote
{
    public class CaptureController : WebApiController
    {
        private readonly ISubject<int> _startCaptureSubject;

        public CaptureController(ISubject<int> startCaptureSubject)
        {
            _startCaptureSubject = startCaptureSubject;
        }

        [Route(HttpVerbs.Post, "/capture")]
        public async Task<string> Capture()
        {
            _startCaptureSubject.OnNext(0);

            return "ok";
        }

    }
}
