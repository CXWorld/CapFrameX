using CapFrameX.Webservice.Data.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace CapFrameX.Webservice.Host.Attributes
{

    public class UserAgentFilter : IActionFilter
    {

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if(!context.HttpContext.Request.HasCXClientHeader())
            {
                context.Result = new RedirectResult("https://capframex.com", false);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
