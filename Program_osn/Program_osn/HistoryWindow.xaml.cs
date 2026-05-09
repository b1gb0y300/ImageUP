using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImageEnhancementWpf
{
    public partial class HistoryWindow : Window
    {
        private static readonly string ThumbnailDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ImageUP", "thumbnails");

        public HistoryWindow()
        {
            InitializeComponent();
            Loaded += HistoryWindow_Loaded;
        }

        private void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshCards();
        }

        private void RefreshCards()
        {
            var history = AppSettings.Current.History;

            if (history.Count == 0)
            {
                SubtitleText.Text     = "Нет записей";
                EmptyPanel.Visibility = Visibility.Visible;
                CardsScrollViewer.Visibility = Visibility.Collapsed;
                ClearAllButton.IsEnabled = false;
                return;
            }

            SubtitleText.Text     = $"{history.Count} {PluralEntries(history.Count)}";
            EmptyPanel.Visibility = Visibility.Collapsed;
            CardsScrollViewer.Visibility = Visibility.Visible;
            ClearAllButton.IsEnabled = true;

            CardsPanel.Children.Clear();
            foreach (var entry in history)
                CardsPanel.Children.Add(BuildCard(entry));

            BuildStatsPanel();
        }

        private FrameworkElement BuildCard(ProcessingHistoryEntry entry)
        {
            // Корневой контейнер карточки
            var card = new Border
            {
                Width           = 188,
                Margin          = new Thickness(0, 0, 12, 12),
                CornerRadius    = new CornerRadius(14),
                Background      = (Brush)FindResource("SurfaceInputBrush"),
                BorderBrush     = (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(1),
                ClipToBounds    = true
            };
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.15
            };

            var stack = new StackPanel();
            card.Child = stack;

            // Миниатюра
            var thumbBorder = new Border
            {
                Height     = 120,
                Background = (Brush)FindResource("ImageViewerBrush"),
                ClipToBounds = true
            };

            var thumbSource = LoadThumbnail(entry.ThumbnailFileName);
            if (thumbSource != null)
            {
                thumbBorder.Child = new System.Windows.Controls.Image
                {
                    Source  = thumbSource,
                    Stretch = Stretch.UniformToFill
                };
            }
            else
            {
                // Плейсхолдер
                var placeholder = new TextBlock
                {
                    Text              = GetMethodIcon(entry.MethodId),
                    FontSize          = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Foreground        = (Brush)FindResource("TextMutedBrush")
                };
                thumbBorder.Child = placeholder;
            }
            stack.Children.Add(thumbBorder);

            // Информационный блок
            var info = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };

            // Название метода с иконкой
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            nameRow.Children.Add(new TextBlock
            {
                Text       = GetMethodIcon(entry.MethodId),
                FontSize   = 12,
                Foreground = (Brush)FindResource("AccentBrush"),
                Margin     = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            nameRow.Children.Add(new TextBlock
            {
                Text         = entry.MethodName,
                FontSize     = 12,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = (Brush)FindResource("TextPrimaryBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            info.Children.Add(nameRow);

            // Имя файла
            if (!string.IsNullOrEmpty(entry.ImageFileName))
            {
                info.Children.Add(new TextBlock
                {
                    Text         = entry.ImageFileName,
                    FontSize     = 10,
                    Foreground   = (Brush)FindResource("TextMutedBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin       = new Thickness(0, 0, 0, 6)
                });
            }

            // Метрики SSIM и PSNR
            bool hasSsim = !double.IsNaN(entry.Ssim);
            bool hasPsnr = !double.IsNaN(entry.Psnr);

            if (hasSsim || hasPsnr)
            {
                var metricsRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin      = new Thickness(0, 0, 0, 6)
                };

                if (hasSsim)
                {
                    metricsRow.Children.Add(BuildMetricBadge(
                        "SSIM", entry.Ssim.ToString("F3"), "#22C55E", "#14532D"));
                }
                if (hasPsnr)
                {
                    metricsRow.Children.Add(BuildMetricBadge(
                        "PSNR", $"{entry.Psnr:F1} dB", "#6366F1", "#312E81",
                        hasSsim ? new Thickness(6, 0, 0, 0) : new Thickness(0)));
                }
                info.Children.Add(metricsRow);
            }

            // Дата/время
            info.Children.Add(new TextBlock
            {
                Text       = FormatTimestamp(entry.Timestamp),
                FontSize   = 10,
                Foreground = (Brush)FindResource("TextMutedBrush")
            });

            stack.Children.Add(info);
            return card;
        }

        private static Border BuildMetricBadge(string label, string value, string fg, string bg,
            Thickness margin = default)
        {
            var badge = new Border
            {
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(6, 3, 6, 3),
                Margin          = margin,
                Background      = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(bg) with { A = 50 })
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock
            {
                Text       = label + " ",
                FontSize   = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)),
                VerticalAlignment = VerticalAlignment.Center
            });
            row.Children.Add(new TextBlock
            {
                Text       = value,
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg)),
                VerticalAlignment = VerticalAlignment.Center
            });

            badge.Child = row;
            return badge;
        }

        private static BitmapImage? LoadThumbnail(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            var path = Path.Combine(ThumbnailDir, fileName);
            if (!File.Exists(path)) return null;
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption  = BitmapCacheOption.OnLoad;
                bi.UriSource    = new Uri(path, UriKind.Absolute);
                bi.DecodePixelWidth = 200;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        private void BuildStatsPanel()
        {
            StatsPanel.Children.Clear();
            var history = AppSettings.Current.History;

            // ── Метка секции ──────────────────────────────────────────
            StatsPanel.Children.Add(new TextBlock
            {
                Text = "СТАТИСТИКА",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Margin = new Thickness(0, 0, 0, 13)
            });

            // ── Общее число обработок ─────────────────────────────────
            var totalCard = new Border
            {
                Background = (Brush)FindResource("SurfaceInputBrush"),
                BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(13),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var totalStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            totalStack.Children.Add(new TextBlock
            {
                Text = history.Count.ToString(),
                FontSize = 32, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            totalStack.Children.Add(new TextBlock
            {
                Text = "обработок",
                FontSize = 11, Foreground = (Brush)FindResource("TextMutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            totalCard.Child = totalStack;
            StatsPanel.Children.Add(totalCard);

            if (history.Count == 0) return;

            // ── Средние метрики ────────────────────────────────────────
            var ssimVals = history.Where(e => !double.IsNaN(e.Ssim)).Select(e => e.Ssim).ToList();
            var psnrVals = history.Where(e => !double.IsNaN(e.Psnr)).Select(e => e.Psnr).ToList();

            var metricsGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            void AddMetricCard(int col, string value, string label, string colorHex)
            {
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var card = new Border
                {
                    Background = (Brush)FindResource("SurfaceInputBrush"),
                    BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8)
                };
                var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = value, FontSize = 16, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                sp.Children.Add(new TextBlock
                {
                    Text = label, FontSize = 9,
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                card.Child = sp;
                Grid.SetColumn(card, col);
                metricsGrid.Children.Add(card);
            }

            AddMetricCard(0,
                ssimVals.Any() ? ssimVals.Average().ToString("F3") : "—",
                "ср. SSIM", "#10B981");
            AddMetricCard(2,
                psnrVals.Any() ? $"{psnrVals.Average():F1}" : "—",
                "PSNR dB", "#6366F1");
            StatsPanel.Children.Add(metricsGrid);

            // ── Топ методы ──────────────────────────────────────────
            StatsPanel.Children.Add(new TextBlock
            {
                Text = "ТОП МЕТОДЫ",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                Margin = new Thickness(0, 13, 0, 8)
            });

            var topMethods = history
                .GroupBy(e => e.MethodName)
                .OrderByDescending(g => g.Count())
                .Take(5);

            int maxCnt = history.GroupBy(e => e.MethodName).Max(g => g.Count());

            foreach (var grp in topMethods)
            {
                int cnt = grp.Count();
                var row = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var bar = new Border
                {
                    Height = 24, CornerRadius = new CornerRadius(4),
                    Background = (Brush)FindResource("SurfaceInputBrush"),
                    Margin = new Thickness(0, 0, 8, 0),
                    ClipToBounds = true
                };

                var barContent = new Grid();
                var fill = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Background = (Brush)FindResource("AccentBrush"),
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var barLabel = new TextBlock
                {
                    Text = grp.Key, FontSize = 10,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                barContent.Children.Add(fill);
                barContent.Children.Add(barLabel);
                bar.Child = barContent;

                // Set fill width after layout
                double ratio = maxCnt > 0 ? (double)cnt / maxCnt : 0;
                bar.Loaded += (_, _) => fill.Width = bar.ActualWidth * ratio;

                Grid.SetColumn(bar, 0);
                var countText = new TextBlock
                {
                    Text = cnt.ToString(), FontSize = 10,
                    Foreground = (Brush)FindResource("TextMutedBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(countText, 1);
                row.Children.Add(bar);
                row.Children.Add(countText);
                StatsPanel.Children.Add(row);
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Очистить всю историю обработок и миниатюры?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            // Удаляем миниатюры
            foreach (var entry in AppSettings.Current.History)
            {
                if (!string.IsNullOrEmpty(entry.ThumbnailFileName))
                {
                    var path = Path.Combine(ThumbnailDir, entry.ThumbnailFileName);
                    try { if (File.Exists(path)) File.Delete(path); } catch { }
                }
            }

            AppSettings.Current.History.Clear();
            AppSettings.Current.Save();
            RefreshCards();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private static string FormatTimestamp(DateTime dt)
        {
            var now = DateTime.Now;
            if (dt.Date == now.Date)
                return $"Сегодня, {dt:HH:mm}";
            if (dt.Date == now.Date.AddDays(-1))
                return $"Вчера, {dt:HH:mm}";
            return dt.ToString("dd.MM.yyyy HH:mm");
        }

        private static string PluralEntries(int count)
        {
            int mod = count % 100;
            if (mod >= 11 && mod <= 19) return "записей";
            int last = count % 10;
            if (last == 1) return "запись";
            if (last >= 2 && last <= 4) return "записи";
            return "записей";
        }

        private static string GetMethodIcon(string methodId) => methodId switch
        {
            "LinearContrast"  => "☰",
            "HistogramEq"     => "📊",
            "MedianFilter"    => "◇",
            "WienerFilter"    => "⚙",
            "UnsharpMask"     => "✴",
            "BilateralFilter" => "◯",
            "Gamma"           => "☼",
            "EdgeDetection"   => "✂",
            "Morphology"      => "▢",
            "DenoiseNLM"      => "≋",
            "Deblur"          => "🎯",
            "SeamCarving"     => "↔",
            "ESRGAN"          => "✨",
            "CLAHE"           => "⬛",
            "MSRetinex"       => "🌅",
            _                 => "•"
        };
    }
}
