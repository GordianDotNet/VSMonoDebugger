using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Linq;
using VSMonoDebugger.Views;

namespace VSMonoDebugger.Settings
{
    public class UserSettingsContainer : BaseViewModel
    {
        public UserSettingsContainer()
        {
        }

        private string _selectedId;

        public string SelectedId
        {
            get
            {
                return _selectedId;
            }
            set
            {
                _selectedId = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(CurrentUserSettings));
            }
        }

        private ObservableCollection<UserSettings> _deviceConnections = new ObservableCollection<UserSettings>();

        public ObservableCollection<UserSettings> DeviceConnections
        {
            get
            {
                return _deviceConnections;
            }
            set
            {
                _deviceConnections = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(CurrentUserSettings));
                NotifyPropertyChanged(nameof(SelectedId));
            }
        }

        private UserSettings _currentUserSettings;

        public UserSettings CurrentUserSettings
        {
            get
            {
                if (_currentUserSettings == null || _currentUserSettings.Id != SelectedId)
                {
                    _currentUserSettings = DeviceConnections?.Where(x => x.Id == SelectedId).FirstOrDefault() ?? new UserSettings();
                }
                return _currentUserSettings;
            }
        }

        public string SerializeToJson()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public static UserSettingsContainer DeserializeFromJson(string json)
        {
            var result = JsonConvert.DeserializeObject<UserSettingsContainer>(json) ?? new UserSettingsContainer();
            Validate(result);
            return result;
        }

        private static void Validate(UserSettingsContainer instance)
        {
            instance.DeviceConnections = instance.DeviceConnections ?? new ObservableCollection<UserSettings>();

            if (instance.DeviceConnections == null || instance.DeviceConnections.Count == 0)
            {
                instance.DeviceConnections.Add(new UserSettings());
            }
            if (string.IsNullOrWhiteSpace(instance.SelectedId) || !instance.DeviceConnections.Any(x => x.Id == instance.SelectedId))
            {
                instance.SelectedId = instance.DeviceConnections.First().Id;
            }
            foreach (var deviceConnection in instance.DeviceConnections)
            {
                if (deviceConnection.SSHMonoDebugPort <= 0)
                {
                    deviceConnection.SSHMonoDebugPort = UserSettings.DEFAULT_DEBUGGER_AGENT_PORT;
                }
            }
        }
    }
}