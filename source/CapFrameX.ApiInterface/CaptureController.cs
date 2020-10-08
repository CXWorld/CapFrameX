using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Remote.JsonPayload;
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
        private readonly CaptureManager _captureManager;

        public CaptureController(CaptureManager captureManager)
        {
            _captureManager = captureManager;
        }

        [Route(HttpVerbs.Post, "/capture")]
        public async Task<string> StartCapture()
        {
            var parameters = await HttpContext.GetRequestDataAsync<StartCapturePayload>();

            await _captureManager.StartCapture(new CaptureOptions() {
                CaptureTime = parameters.CaptureTime,
                CaptureFileMode = parameters.CaptureFileMode,
                ProcessName = parameters.ProcessName,
                RecordDirectory = parameters.RecordDirectory
            });

            return "ok";
        }

        [Route(HttpVerbs.Delete, "/capture")]
        public async Task<string> StopCapture()
        {
            await _captureManager.StopCapture();

            return "ok";
        }

        [Route(HttpVerbs.Get, "/processes")]
        public async Task<IEnumerable<string>> GetProcesses()
        {
            return _captureManager.GetAllFilteredProcesses(new HashSet<string>());
        }
    }
}
