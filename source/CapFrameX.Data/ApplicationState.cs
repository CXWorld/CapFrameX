using CapFrameX.Contracts.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    public class ApplicationState
    {
        public IList<IFileRecordInfo> SelectedRecords { get; set; }
    }
}
