using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Views
{
    public class DebugSettingsModel : BaseViewModel
    {
        private UserSettingsContainer _settingsContainer;

        public DebugSettingsModel()
        {
            _settingsContainer = UserSettingsManager.Instance.Load();
        }

        public void SaveDebugSettings()
        {
            UserSettingsManager.Instance.Save(_settingsContainer);
        }

        public UserSettingsContainer SettingsContainer
        {
            get
            {
                return _settingsContainer;
            }
            set
            {
                _settingsContainer = value;
                NotifyPropertyChanged();
            }
        }
    }
}