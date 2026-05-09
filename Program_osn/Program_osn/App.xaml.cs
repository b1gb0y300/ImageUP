using System.Windows;

namespace ImageEnhancementWpf
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Применяем сохранённую тему до открытия первого окна
            ThemeManager.Apply();
            new WelcomeWindow().Show();
        }
    }
}
