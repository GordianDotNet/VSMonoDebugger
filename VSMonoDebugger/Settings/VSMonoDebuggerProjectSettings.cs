using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSMonoDebugger.Settings
{
    public struct VSMonoDebuggerProjectSettings
    {
        public string SSHDeployPath;

        public string SerializeToJson()
        {
            var json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static VSMonoDebuggerProjectSettings DeserializeFromJson(string json)
        {
            var result = JsonConvert.DeserializeObject<VSMonoDebuggerProjectSettings>(json);
            //Validate(result);
            return result;
        }
    }
}
