using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace CapFrameX.Extensions.NetStandard
{
    public static class JTokenExtensions
    {
        public static T GetValue<T>(this JToken token, string key, StringComparison keyComparison = StringComparison.OrdinalIgnoreCase)
        {
            foreach(var child in token.Children())
            {
                if(child.Path.Equals(key, keyComparison))
                {
                    return token.Value<T>(child.Path);
                }
            }
            return default(T);
        }
    }
}
