using System;
using System.Collections.ObjectModel;
using System.Linq;
using VSMonoDebugger.Settings;

namespace VSMonoDebugger.Views
{
    public class DebugSettingsModel : BaseViewModel
    {
        private UserSettingsContainer _settingsContainer;

        public DebugSettingsModel()
        {
            _allRedirectOutputOptions = new ObservableCollection<RedirectOutputOptions>(Enum.GetValues(typeof(RedirectOutputOptions)).Cast<RedirectOutputOptions>());
            _settingsContainer = UserSettingsManager.Instance.Load();
        }

        public void SaveDebugSettings()
        {
            UserSettingsManager.Instance.Save(_settingsContainer);
        }

        public void LoadFromDebugSettings(string path)
        {
            SettingsContainer = UserSettingsManager.Instance.LoadFromPath(path);
        }

        public void SaveAsDebugSettings(string path)
        {
            UserSettingsManager.Instance.SaveAs(_settingsContainer, path);
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

        private ObservableCollection<RedirectOutputOptions> _allRedirectOutputOptions = new ObservableCollection<RedirectOutputOptions>();
        public ObservableCollection<RedirectOutputOptions> AllRedirectOutputOptions
        {
            get
            {
                return _allRedirectOutputOptions;
            }
            set
            {
                _allRedirectOutputOptions = value;
                NotifyPropertyChanged();
            }
        }
    }
}