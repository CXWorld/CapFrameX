using CapFrameX.Contracts.Data;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Remote
{
    public class VersionController : WebApiController
    {
        private readonly IAppVersionProvider _appVersionProvider;

        public VersionController(IAppVersionProvider appVersionProvider)
        {
            _appVersionProvider = appVersionProvider;
        }

        [Route(HttpVerbs.Get, "/version")]
        public object GetVersion()
        {
            var version = _appVersionProvider.GetAppVersion();
            return new { Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" };
        }
    }
}
