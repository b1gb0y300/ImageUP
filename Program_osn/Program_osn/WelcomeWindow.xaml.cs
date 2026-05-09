using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImageEnhancementWpf
{
    public partial class WelcomeWindow : Window
    {
        public WelcomeWindow()
        {
            InitializeComponent();
            Loaded += WelcomeWindow_Loaded;
            this.Opacity = 1.0;
        }

        private void WelcomeWindow_Loaded(object sender, RoutedEventArgs e) { }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var main = new MainWindow();
            main.Show();
            Close();
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var history = new HistoryWindow { Owner = this };
            history.ShowDialog();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new HelpWindow();
            help.Owner = this;
            help.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow { Owner = this };
            settings.ShowDialog();
        }
    }
}
