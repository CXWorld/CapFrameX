using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Data.Interfaces
{
    public interface ICrashlogReportingService
    {
        Task<Guid> UploadCrashlog(byte[] data, string filename);
    }
}
