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
            RedirectOutputOption = RedirectOutputOptions.RedirectStandardOutput;
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

        private RedirectOutputOptions _redirectOutputOption;
        public RedirectOutputOptions RedirectOutputOption { get => _redirectOutputOption; set { _redirectOutputOption = value; NotifyPropertyChanged(); } }

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

        private string _preDebugScriptWithParameters;
        public string PreDebugScriptWithParameters
        {
            get
            {
                return string.IsNullOrWhiteSpace(_preDebugScriptWithParameters) ? DefaultPreDebugScriptWithParameters : _preDebugScriptWithParameters;
            }
            set
            {
                _preDebugScriptWithParameters = value;
                NotifyPropertyChanged();
            }
        }

        private string _debugScriptWithParameters;
        public string DebugScriptWithParameters
        {
            get
            {
                return string.IsNullOrWhiteSpace(_debugScriptWithParameters) ? DefaultDebugScriptWithParameters : _debugScriptWithParameters;
            }
            set
            {
                _debugScriptWithParameters = value;
                NotifyPropertyChanged();
            }
        }

        public readonly string MONO_DEBUG_PORT = "$(MONO_DEBUG_PORT)";
        public readonly string TARGET_EXE_FILENAME = "$(TARGET_EXE_FILENAME)";
        public readonly string START_ARGUMENTS = "$(START_ARGUMENTS)";

        public string DefaultPreDebugScriptWithParameters
        {
            get
            {
                return $"kill $(lsof -i | grep 'mono' | grep '\\*:{MONO_DEBUG_PORT}' | awk '{{print $2}}');\r\nkill $(ps w | grep '[m]ono --debugger-agent=address' | awk '{{print $1}}');";
            }
        }

        public string DefaultDebugScriptWithParameters
        {
            get
            {
                return $"mono --debugger-agent=address=0.0.0.0:{MONO_DEBUG_PORT},transport=dt_socket,server=y --debug=mdb-optimizations {TARGET_EXE_FILENAME} {START_ARGUMENTS} &";
            }
        }

        public string SupportedScriptParameters
        {
            get
            {
                return $@"1) An empty script will replaced by the default script.
2) Windows new line '\r\n' will be replaced by '\n'.
3) You can use following Parameters in the debug scripts:
{MONO_DEBUG_PORT} = Will be replaced by the mono debug port.
{TARGET_EXE_FILENAME} = Replaced by the application name (*.exe) results from the StartupProject.
{START_ARGUMENTS} = Is replaced by the startup parameters set in the properties of the StartupProject.";                
            }
        }
    }

    [Flags]
    public enum RedirectOutputOptions
    {
        None = 0,
        RedirectStandardOutput = 1,
        RedirectErrorOutput = 2,
        RedirectAll = RedirectStandardOutput | RedirectErrorOutput
    }
}