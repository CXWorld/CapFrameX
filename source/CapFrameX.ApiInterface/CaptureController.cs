using CapFrameX.Data;
using CapFrameX.Remote.JsonPayload;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace CapFrameX.Remote
{
    public class CaptureController : WebApiController
    {
        private readonly CaptureManager _captureManager;

        public CaptureController(CaptureManager captureManager)
        {
            _captureManager = captureManager;
        }

        [Route(HttpVerbs.Post, "/capture")]
        public async Task<object> StartCapture()
        {
            try
            {
                var parameters = await HttpContext.GetRequestDataAsync<StartCapturePayload>();

                await _captureManager.StartCapture(new CaptureOptions()
                {
                    CaptureTime = parameters.CaptureTime,
                    CaptureFileMode = parameters.CaptureFileMode,
                    ProcessName = parameters.ProcessName,
                    RecordDirectory = parameters.RecordDirectory,
                    Remote = true
                });

                return new
                {
                    Message = "Capture started"
                };
            }
            catch (Exception ecx)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new
                {
                    Message = ecx.Message
                };
            }
        }

        [Route(HttpVerbs.Delete, "/capture")]
        public async Task<object> StopCapture()
        {
            try
            {
                await _captureManager.StopCapture();

                return new
                {
                    Message = "Capture stopped"
                };
            }
            catch (Exception ecx)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return new
                {
                    Message = ecx.Message
                };
            }
        }

        [Route(HttpVerbs.Get, "/processes")]
        public async Task<IEnumerable<string>> GetProcesses()
        {
            return _captureManager.GetAllFilteredProcesses(new HashSet<string>());
        }
    }
}
