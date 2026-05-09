using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using ImageEnhancement;

namespace ImageEnhancementWpf
{
    public partial class QualityWindow : Window
    {
        public QualityWindow(Bitmap? original, Bitmap? processed)
        {
            InitializeComponent();

            if (original == null || processed == null)
            {
                SsimValueText.Text = "—";
                PsnrValueText.Text = "—";
                QualityHintText.Text = original == null
                    ? "Загрузите изображение на главном экране и примените метод обработки."
                    : "Примените метод обработки — результат появится в блоке «После обработки».";
                return;
            }

            bool sameSize = original.Width == processed.Width && original.Height == processed.Height;

            // SSIM
            if (sameSize)
            {
                try
                {
                    double ssim = Metrics.ComputeSSIM(original, processed);
                    SsimValueText.Text = ssim.ToString("F4");
                }
                catch { SsimValueText.Text = "ошибка"; }
            }
            else
            {
                SsimValueText.Text = "— (разный размер)";
            }

            // PSNR
            if (sameSize)
            {
                try
                {
                    double psnr = Metrics.ComputePSNR(original, processed);
                    PsnrValueText.Text = psnr.ToString("F2") + " dB";
                }
                catch { PsnrValueText.Text = "ошибка"; }
            }
            else
            {
                PsnrValueText.Text = "— (разный размер)";
            }

            // Sharpness — работает всегда, без эталона
            try
            {
                double sharpOrig = Metrics.ComputeSharpness(original);
                double sharpProc = Metrics.ComputeSharpness(processed);
                SharpOrigText.Text = FormatSharpness(sharpOrig);
                SharpProcText.Text = FormatSharpness(sharpProc);

                if (sharpOrig > 0)
                {
                    double delta = (sharpProc - sharpOrig) / sharpOrig * 100;
                    if (delta > 0.5)
                    {
                        SharpDeltaText.Text = $"▲ +{delta:F0}%";
                        SharpDeltaText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
                    }
                    else if (delta < -0.5)
                    {
                        SharpDeltaText.Text = $"▼ {delta:F0}%";
                        SharpDeltaText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                    }
                    else
                    {
                        SharpDeltaText.Text = "≈ без изменений";
                        SharpDeltaText.SetResourceReference(ForegroundProperty, "TextMutedBrush");
                    }
                }
            }
            catch
            {
                SharpOrigText.Text = "—";
                SharpProcText.Text = "—";
            }

            QualityHintText.Text = sameSize
                ? "SSIM и PSNR сравнивают результат с оригиналом. Резкость оценивается независимо для каждого изображения."
                : "Размеры изображений различаются (например, после ×4 супер-разрешения). SSIM/PSNR недоступны — используйте метрику Резкости.";
        }

        private static string FormatSharpness(double v)
        {
            if (v >= 10000) return $"{v / 1000:F1}k";
            if (v >= 1000)  return $"{v:F0}";
            return v.ToString("F1");
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
