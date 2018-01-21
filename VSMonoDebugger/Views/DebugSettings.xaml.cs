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
    }
}