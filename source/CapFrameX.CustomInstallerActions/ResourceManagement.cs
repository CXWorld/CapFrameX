using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.CustomInstallerActions
{
    public class ResourceManagement
    {
        [CustomAction]
        public static ActionResult CleanupConfigResources(Session session)
        {
            session.Log("Begin RemoveAutoStartKey");

            try
            {
               
            }
            catch { }

            return ActionResult.Success;
        }
    }
}
