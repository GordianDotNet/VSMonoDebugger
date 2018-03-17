using VSMonoDebugger.Settings;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSMonoDebugger.Views
{
    public class DebugSettingsModel : INotifyPropertyChanged
    {
        UserSettings _settings;

        public DebugSettingsModel()
        {
            _settings = UserSettingsManager.Instance.Load();
            SSHMonoDebugPort = _settings.SSHMonoDebugPort <= 0 ? UserSettings.DEFAULT_DEBUGGER_AGENT_PORT : _settings.SSHMonoDebugPort;            
        }

        public void SaveDebugSettings()
        {
            UserSettings settings = UserSettingsManager.Instance.Load();
            settings.SSHUsername = SSHUsername;
            settings.SSHPassword = SSHPassword;
            settings.SSHHostIP = SSHHostIP;
            settings.SSHPort = SSHPort;
            settings.SSHDeployPath = SSHDeployPath;
            settings.SSHMonoDebugPort = SSHMonoDebugPort;
            settings.SSHPdb2mdbCommand = SSHPdb2mdbCommand;
            settings.SSHDebugConnectionTimeout = SSHDebugConnectionTimeout;
            UserSettingsManager.Instance.Save(settings);
        }

        public string SSHFullUrl
        {
            get
            {
                return $"{SSHUsername}@{SSHHostIP}:{SSHPort}";
            }
            set
            {

            }
        }

        public string SSHHostIP
        {
            get
            {
                return _settings.SSHHostIP;
            }
            set
            {
                _settings.SSHHostIP = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(SSHFullUrl));
            }
        }

        public int SSHPort
        {
            get
            {
                return _settings.SSHPort;
            }
            set
            {
                _settings.SSHPort = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(SSHFullUrl));
            }
        }

        public string SSHUsername
        {
            get
            {
                return _settings.SSHUsername;
            }
            set
            {
                _settings.SSHUsername = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(SSHFullUrl));
            }
        }

        public string SSHPassword
        {
            get
            {
                // TODO add encryption
                return _settings.SSHPassword;
            }
            set
            {
                // TODO add decryption
                _settings.SSHPassword = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(SSHFullUrl));
            }
        }

        public string SSHDeployPath
        {
            get
            {
                return _settings.SSHDeployPath;
            }
            set
            {
                _settings.SSHDeployPath = value;
                NotifyPropertyChanged();
            }
        }

        public int SSHMonoDebugPort
        {
            get
            {
                return _settings.SSHMonoDebugPort;
            }
            set
            {
                _settings.SSHMonoDebugPort = value;
                NotifyPropertyChanged();
            }
        }
        public string SSHPdb2mdbCommand
        {
            get
            {
                return _settings.SSHPdb2mdbCommand;
            }
            set
            {
                _settings.SSHPdb2mdbCommand = value;
                NotifyPropertyChanged();
            }
        }
        public int SSHDebugConnectionTimeout
        {
            get
            {
                return _settings.SSHDebugConnectionTimeout;
            }
            set
            {
                _settings.SSHDebugConnectionTimeout = value;
                NotifyPropertyChanged();
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}