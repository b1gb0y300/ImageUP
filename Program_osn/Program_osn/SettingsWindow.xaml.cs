using System;
using System.Windows;
using System.Windows.Controls;

namespace ImageEnhancementWpf
{
    public partial class SettingsWindow : Window
    {
        private bool _suppressEvents;

        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _suppressEvents = true;

            // Тема
            if (ThemeManager.IsLightTheme)
                LightThemeButton.IsChecked = true;
            else
                DarkThemeButton.IsChecked = true;

            // Автопоказ результата
            AutoShowResultToggle.IsChecked = AppSettings.Current.AutoShowResult;

            // Формат по умолчанию
            DefaultFormatCombo.SelectedIndex = AppSettings.Current.DefaultSaveFormat switch
            {
                "jpeg" => 1,
                "bmp"  => 2,
                _      => 0   // png
            };

            // JPEG качество
            JpegQualitySlider.Value = AppSettings.Current.JpegQuality;
            JpegQualityLabel.Text   = $"{AppSettings.Current.JpegQuality}%";

            // Интерполяция
            InterpolationCombo.SelectedIndex = AppSettings.Current.InterpolationMode switch
            {
                "bicubic" => 1,
                "nearest" => 2,
                _         => 0   // bilinear
            };

            // Подтверждение сброса
            ConfirmResetToggle.IsChecked = AppSettings.Current.ConfirmReset;

            _suppressEvents = false;
        }

        // ── Тема ──────────────────────────────────────────────────────────────

        private void DarkThemeButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            ThemeManager.SetTheme(false);
        }

        private void LightThemeButton_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            ThemeManager.SetTheme(true);
        }

        // ── Автопоказ результата ───────────────────────────────────────────────

        private void AutoShowResult_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            AppSettings.Current.AutoShowResult = AutoShowResultToggle.IsChecked == true;
            AppSettings.Current.Save();
        }

        // ── Формат экспорта ───────────────────────────────────────────────────

        private void DefaultFormat_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            AppSettings.Current.DefaultSaveFormat = DefaultFormatCombo.SelectedIndex switch
            {
                1 => "jpeg",
                2 => "bmp",
                _ => "png"
            };
            AppSettings.Current.Save();
        }

        // ── Качество JPEG ──────────────────────────────────────────────────────

        private void JpegQuality_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvents) return;
            int q = (int)JpegQualitySlider.Value;
            JpegQualityLabel.Text = $"{q}%";
            AppSettings.Current.JpegQuality = q;
            AppSettings.Current.Save();
        }

        // ── Интерполяция ──────────────────────────────────────────────────────

        private void Interpolation_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            AppSettings.Current.InterpolationMode = InterpolationCombo.SelectedIndex switch
            {
                1 => "bicubic",
                2 => "nearest",
                _ => "bilinear"
            };
            AppSettings.Current.Save();
        }

        // ── Подтверждение сброса ──────────────────────────────────────────────

        private void ConfirmReset_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            AppSettings.Current.ConfirmReset = ConfirmResetToggle.IsChecked == true;
            AppSettings.Current.Save();
        }

        // ── Сброс всех настроек ───────────────────────────────────────────────

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Сбросить все настройки к значениям по умолчанию?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            AppSettings.Current.AutoShowResult    = true;
            AppSettings.Current.DefaultSaveFormat = "png";
            AppSettings.Current.JpegQuality       = 85;
            AppSettings.Current.InterpolationMode = "bilinear";
            AppSettings.Current.ConfirmReset       = true;
            AppSettings.Current.IsLightTheme       = false;
            AppSettings.Current.Save();

            ThemeManager.SetTheme(false);

            _suppressEvents = true;
            DarkThemeButton.IsChecked          = true;
            AutoShowResultToggle.IsChecked      = true;
            DefaultFormatCombo.SelectedIndex    = 0;
            JpegQualitySlider.Value             = 85;
            JpegQualityLabel.Text               = "85%";
            InterpolationCombo.SelectedIndex    = 0;
            ConfirmResetToggle.IsChecked         = true;
            _suppressEvents = false;
        }
    }
}
