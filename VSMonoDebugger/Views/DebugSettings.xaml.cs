using System.Linq;
using System.Windows;

namespace VSMonoDebugger.Views
{
    /// <summary>
    ///     Interaktionslogik für DebugOverSSH.xaml
    /// </summary>
    public partial class DebugSettings : Window
    {
        public DebugSettings()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            ViewModel = new DebugSettingsModel();
            DataContext = ViewModel;
            //Closing += (o, e) => ViewModel.SaveDebugSettings();
        }

        public DebugSettingsModel ViewModel { get; set; }

        private void Save(object sender, RoutedEventArgs e)
        {
            ViewModel.SaveDebugSettings();
            DialogResult = true;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SshPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SettingsContainer?.CurrentUserSettings != null)
            {
                ViewModel.SettingsContainer.CurrentUserSettings.SSHPassword = SshPasswordBox.Password;
            }
        }

        private void DeviceConnections_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ViewModel?.SettingsContainer?.CurrentUserSettings?.SSHPassword != null)
            {
                SshPasswordBox.Password = ViewModel?.SettingsContainer?.CurrentUserSettings?.SSHPassword;
            }
        }

        private void Add(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                var newUserSettings = new Settings.UserSettings();
                ViewModel.SettingsContainer.DeviceConnections.Add(newUserSettings);
                ViewModel.SettingsContainer.SelectedId = newUserSettings.Id;
            }
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SettingsContainer?.DeviceConnections?.Count > 1)
            {
                var currentUserSettings = ViewModel.SettingsContainer.CurrentUserSettings;
                ViewModel.SettingsContainer.DeviceConnections.Remove(currentUserSettings);
                ViewModel.SettingsContainer.SelectedId = ViewModel.SettingsContainer.DeviceConnections.First().Id;
            }
        }

        private void RedirectOutputOption_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}