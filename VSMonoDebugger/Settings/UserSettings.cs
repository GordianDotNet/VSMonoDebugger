using Newtonsoft.Json;

namespace VSMonoDebugger.Settings
{
    public class UserSettings
    {
        public readonly static int DEFAULT_DEBUGGER_AGENT_PORT = 11000;

        public UserSettings()
        {
            LastIp = "127.0.0.1";
            LastTimeout = 10000;

            SSHHostIP = "127.0.0.1";
            SSHPort = 22;
            SSHUsername = string.Empty;
            SSHPassword = string.Empty;
            SSHDeployPath = "./MonoDebugTemp/";
            SSHMonoDebugPort = DEFAULT_DEBUGGER_AGENT_PORT;
            SSHPdb2mdbCommand = "mono /usr/lib/mono/4.5/pdb2mdb.exe";
            SSHDebugConnectionTimeout = 20;
        }

        public string SerializeToJson()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static UserSettings DeserializeFromJson(string json)
        {
            var result = JsonConvert.DeserializeObject<UserSettings>(json);
            return result;
        }

        public string LastIp { get; set; }
        public int LastTimeout { get; set; }

        public string SSHHostIP { get; set; }
        public int SSHPort { get; set; }
        public string SSHUsername { get; set; }
        public string SSHPassword { get; set; }
        public string SSHDeployPath { get; set; }
        public int SSHMonoDebugPort { get; set; }
        public string SSHPdb2mdbCommand { get; set; }
        public int SSHDebugConnectionTimeout { get; set; }
    }
}