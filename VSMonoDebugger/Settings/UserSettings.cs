using System;
using VSMonoDebugger.Views;

namespace VSMonoDebugger.Settings
{
    public class UserSettings : BaseViewModel
    {
        public readonly static int DEFAULT_DEBUGGER_AGENT_PORT = 11000;

        private string _id;
        private string _lastIp;
        private int _lastTimeout;
        private string _sSHHostIP;
        private int _sSHPort;
        private string _sSHUsername;
        private string _sSHPassword;
        private string _sSHDeployPath;
        private int _sSHMonoDebugPort;
        private string _sSHPdb2mdbCommand;
        private int _sSHDebugConnectionTimeout;

        public UserSettings()
        {
            Id = Guid.NewGuid().ToString();

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

        public string Id { get => _id; set { _id = value; NotifyPropertyChanged(); } }

        public string LastIp { get => _lastIp; set { _lastIp = value; NotifyPropertyChanged(); } }
        public int LastTimeout { get => _lastTimeout; set { _lastTimeout = value; NotifyPropertyChanged(); } }

        public string SSHHostIP { get => _sSHHostIP; set { _sSHHostIP = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(SSHFullUrl)); } }
        public int SSHPort { get => _sSHPort; set { _sSHPort = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(SSHFullUrl)); } }
        public string SSHUsername { get => _sSHUsername; set { _sSHUsername = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(SSHFullUrl)); } }
        public string SSHPassword { get => _sSHPassword; set { _sSHPassword = value; NotifyPropertyChanged(); } }
        public string SSHDeployPath { get => _sSHDeployPath; set { _sSHDeployPath = value; NotifyPropertyChanged(); } }
        public int SSHMonoDebugPort { get => _sSHMonoDebugPort; set { _sSHMonoDebugPort = value; NotifyPropertyChanged(); } }
        public string SSHPdb2mdbCommand { get => _sSHPdb2mdbCommand; set { _sSHPdb2mdbCommand = value; NotifyPropertyChanged(); } }
        public int SSHDebugConnectionTimeout { get => _sSHDebugConnectionTimeout; set { _sSHDebugConnectionTimeout = value; NotifyPropertyChanged(); } }

        public string SSHFullUrl
        {
            get
            {
                return $"{SSHUsername}@{SSHHostIP}:{SSHPort}";
            }
            set
            {
                NotifyPropertyChanged();
            }
        }
    }
}