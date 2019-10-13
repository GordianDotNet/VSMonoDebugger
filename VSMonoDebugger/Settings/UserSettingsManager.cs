using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using NLog;
using System;

namespace VSMonoDebugger.Settings
{
    public class UserSettingsManager
    {
        public readonly static string SETTINGS_STORE_NAME = "VSMonoDebugger";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private WritableSettingsStore _settingsStore;

        private UserSettingsManager()
        {
        }        

        public UserSettingsContainer Load()
        {
            var result = new UserSettingsContainer();

            if (_settingsStore.CollectionExists(SETTINGS_STORE_NAME))
            {
                try
                {
                    var content = _settingsStore.GetString(SETTINGS_STORE_NAME, "Settings");
                    result = UserSettingsContainer.DeserializeFromJson(content);
                    return result;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            }

            return result;
        }

        public void Save(UserSettingsContainer settings)
        {
            var json = settings.SerializeToJson();
            if (!_settingsStore.CollectionExists(SETTINGS_STORE_NAME))
            {
                _settingsStore.CreateCollection(SETTINGS_STORE_NAME);
            }
            _settingsStore.SetString(SETTINGS_STORE_NAME, "Settings", json);
        }

        public static UserSettingsManager Instance { get; } = new UserSettingsManager();

        public static void Initialize(Package package)
        {
            if (package != null)
            {
                var settingsManager = new ShellSettingsManager(package);
                var configurationSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
                Instance._settingsStore = configurationSettingsStore;
            }
            else
            {
                throw new ArgumentNullException("package argument was null");
            }
        }
    }
}