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
            SshPasswordBox.Password = ViewModel.SSHPassword;
            DataContext = ViewModel;
            Closing += (o, e) => ViewModel.SaveDebugSettings();
        }

        public DebugSettingsModel ViewModel { get; set; }

        private void Save(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void SshPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SSHPassword = SshPasswordBox.Password;
            }
        }
    }
}