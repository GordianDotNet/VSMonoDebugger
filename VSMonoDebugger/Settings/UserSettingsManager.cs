using System;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using NLog;

namespace VSMonoDebugger.Settings
{
    public class UserSettingsManager
    {
        public readonly static string SETTINGS_STORE_NAME = "VSMonoDebugger";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private static readonly UserSettingsManager manager = new UserSettingsManager();
        private WritableSettingsStore store;

        private UserSettingsManager()
        {
        }

        public static UserSettingsManager Instance
        {
            get { return manager; }
        }

        public UserSettings Load()
        {
            var result = new UserSettings();

            if (store.CollectionExists(SETTINGS_STORE_NAME))
            {
                try
                {
                    string content = store.GetString(SETTINGS_STORE_NAME, "Settings");
                    result = UserSettings.DeserializeFromJson(content);
                    return result;
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                }
            }

            return result;
        }

        public void Save(UserSettings settings)
        {
            string json = settings.SerializeToJson();
            if (!store.CollectionExists(SETTINGS_STORE_NAME))
            {
                store.CreateCollection(SETTINGS_STORE_NAME);
            }
            store.SetString(SETTINGS_STORE_NAME, "Settings", json);
        }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            var settingsManager = new ShellSettingsManager(serviceProvider);
            var configurationSettingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
            Instance.store = configurationSettingsStore;
        }
    }
}