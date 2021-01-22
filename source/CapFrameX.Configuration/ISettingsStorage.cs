using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Configuration
{
    public interface ISettingsStorage
    {
        Task Load();
        T GetValue<T>(string key);
        void SetValue(string key, object value);
    }
}
