using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using ImageEnhancement;

namespace ImageEnhancementWpf
{
    public partial class MethodWindow : Window
    {
        private readonly string _methodId;
        private Bitmap? _sourceBitmap;
        private Bitmap? _resultBitmap;

        private Slider? _wienerRadiusSlider;
        private TextBox? _wienerNoiseTextBox;
        private Slider? _gammaSlider;
        private Slider? _unsharpStrengthSlider;
        private Slider? _unsharpRadiusSlider;
        private Slider? _bilateralRadiusSlider;
        private Slider? _bilateralSigmaRSlider;
        private ComboBox? _morphologyCombo;
        private Slider? _morphologyRadiusSlider;
        private Slider? _cannyLowSlider;
        private Slider? _cannyHighSlider;
        private Slider? _nlmSigmaSlider;
        private Slider? _deblurIterationsSlider;
        private Slider? _seamCarvingColsSlider;
        private Slider? _seamCarvingRowsSlider;

        private static readonly string[] AvailableIds = {
            "LinearContrast", "HistogramEq", "MedianFilter", "WienerFilter", "Gamma", "UnsharpMask",
            "BilateralFilter", "Morphology", "EdgeDetection", "DenoiseNLM", "Deblur", "SeamCarving"
        };

        public Bitmap? ResultBitmap => _resultBitmap;

        public MethodWindow(string methodId, Bitmap? sourceBitmap = null)
        {
            InitializeComponent();
            _methodId = methodId;
            _sourceBitmap = sourceBitmap;

            Title = GetMethodTitle(methodId);
            MethodTitleText.Text = GetMethodTitle(methodId);

            UpdateSourcePreview();
            BuildParameters(methodId);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        private void UpdateSourcePreview()
        {
            if (_sourceBitmap != null)
            {
                SourceImageControl.Source = MainWindow.BitmapToImageSource(_sourceBitmap);
                SourceCaptionText.Text = "Исходное с главного окна";
                SourceSizeText.Text = $"{_sourceBitmap.Width} × {_sourceBitmap.Height} px";
                LoadSourceButton.Content = "Загрузить другое";
            }
            else
            {
                SourceCaptionText.Text = "Загрузите изображение";
                SourceSizeText.Text = "";
                LoadSourceButton.Content = "Загрузить изображение";
            }
        }

        private static string GetMethodTitle(string id) => id switch
        {
            "LinearContrast" => "Линейное контрастирование",
            "HistogramEq" => "Эквализация гистограммы",
            "MedianFilter" => "Медианный фильтр",
            "WienerFilter" => "Фильтр Винера",
            "UnsharpMask" => "Контурная резкость (Unsharp Mask)",
            "BilateralFilter" => "Двусторонний фильтр",
            "Morphology" => "Морфологические операции",
            "Gamma" => "Гамма-коррекция",
            "EdgeDetection" => "Детекция границ (Канни)",
            "DenoiseNLM" => "Шумоподавление (NLM)",
            "Deblur" => "Размытие по дефокусу",
            "SeamCarving" => "Seam Carving (контент-адаптивное сжатие)",
            _ => "Метод обработки"
        };

        private void BuildParameters(string methodId)
        {
            ParametersStack.Children.Clear();

            if (methodId == "WienerFilter")
            {
                var radiusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                radiusPanel.Children.Add(new TextBlock
                {
                    Text = "Радиус окна:",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                _wienerRadiusSlider = new Slider
                {
                    Minimum = 1, Maximum = 3, Value = 1,
                    Width = 100, TickFrequency = 1, IsSnapToTickEnabled = true
                };
                radiusPanel.Children.Add(_wienerRadiusSlider);
                var radiusVal = new TextBlock
                {
                    Text = "1",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush")
                };
                _wienerRadiusSlider.ValueChanged += (_, __) =>
                    radiusVal.Text = ((int)_wienerRadiusSlider.Value).ToString();
                radiusPanel.Children.Add(radiusVal);
                ParametersStack.Children.Add(radiusPanel);

                var noisePanel = new StackPanel { Orientation = Orientation.Horizontal };
                noisePanel.Children.Add(new TextBlock
                {
                    Text = "Дисперсия шума:",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                _wienerNoiseTextBox = new TextBox
                {
                    Text = "10", Width = 70,
                    Style = (Style)FindResource("DarkTextBox")
                };
                noisePanel.Children.Add(_wienerNoiseTextBox);
                ParametersStack.Children.Add(noisePanel);
            }
            else if (methodId == "Gamma")
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                panel.Children.Add(new TextBlock
                {
                    Text = "γ (гамма):",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                _gammaSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 0.3, Maximum = 3, Value = 2.2, Width = 120 };
                panel.Children.Add(_gammaSlider);
                var valTb = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush") };
                _gammaSlider.ValueChanged += (_, __) => valTb.Text = _gammaSlider.Value.ToString("F2");
                valTb.Text = "2.20";
                panel.Children.Add(valTb);
                ParametersStack.Children.Add(panel);
            }
            else if (methodId == "UnsharpMask")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Сила k:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _unsharpStrengthSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 0.5, Maximum = 3, Value = 1.5, Width = 100 };
                p1.Children.Add(_unsharpStrengthSlider);
                var v1 = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush") };
                _unsharpStrengthSlider.ValueChanged += (_, __) => v1.Text = _unsharpStrengthSlider.Value.ToString("F1");
                v1.Text = "1.5";
                p1.Children.Add(v1);
                ParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Радиус размытия:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _unsharpRadiusSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 1, Maximum = 3, Value = 1, Width = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
                p2.Children.Add(_unsharpRadiusSlider);
                var v2 = new TextBlock { Text = "1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 24 };
                _unsharpRadiusSlider.ValueChanged += (_, __) => v2.Text = ((int)_unsharpRadiusSlider.Value).ToString();
                p2.Children.Add(v2);
                ParametersStack.Children.Add(p2);
            }
            else if (methodId == "BilateralFilter")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Радиус:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _bilateralRadiusSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 1, Maximum = 4, Value = 2, Width = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
                p1.Children.Add(_bilateralRadiusSlider);
                var v1 = new TextBlock { Text = "2", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 20 };
                _bilateralRadiusSlider.ValueChanged += (_, __) => v1.Text = ((int)_bilateralRadiusSlider.Value).ToString();
                p1.Children.Add(v1);
                ParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "σ по яркости:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _bilateralSigmaRSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 10, Maximum = 80, Value = 30, Width = 100 };
                p2.Children.Add(_bilateralSigmaRSlider);
                var v2 = new TextBlock { Text = "30", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 28 };
                _bilateralSigmaRSlider.ValueChanged += (_, __) => v2.Text = ((int)_bilateralSigmaRSlider.Value).ToString();
                p2.Children.Add(v2);
                ParametersStack.Children.Add(p2);
            }
            else if (methodId == "Morphology")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Операция:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _morphologyCombo = new ComboBox { Width = 120, SelectedIndex = 0, Style = (Style)FindResource("DarkComboBox") };
                _morphologyCombo.Items.Add("Открытие");
                _morphologyCombo.Items.Add("Закрытие");
                p1.Children.Add(_morphologyCombo);
                ParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Радиус:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _morphologyRadiusSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 1, Maximum = 3, Value = 1, Width = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
                p2.Children.Add(_morphologyRadiusSlider);
                var morphVal = new TextBlock { Text = "1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 24 };
                _morphologyRadiusSlider.ValueChanged += (_, __) => morphVal.Text = ((int)_morphologyRadiusSlider.Value).ToString();
                p2.Children.Add(morphVal);
                ParametersStack.Children.Add(p2);
            }
            else if (methodId == "EdgeDetection")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Нижний порог:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _cannyLowSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 0.05, Maximum = 0.4, Value = 0.15, Width = 100 };
                p1.Children.Add(_cannyLowSlider);
                var lowVal = new TextBlock { Text = "0.15", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 36 };
                _cannyLowSlider.ValueChanged += (_, __) => lowVal.Text = _cannyLowSlider.Value.ToString("F2");
                p1.Children.Add(lowVal);
                ParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Верхний порог:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _cannyHighSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 0.2, Maximum = 0.6, Value = 0.35, Width = 100 };
                p2.Children.Add(_cannyHighSlider);
                var highVal = new TextBlock { Text = "0.35", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 36 };
                _cannyHighSlider.ValueChanged += (_, __) => highVal.Text = _cannyHighSlider.Value.ToString("F2");
                p2.Children.Add(highVal);
                ParametersStack.Children.Add(p2);
            }
            else if (methodId == "DenoiseNLM")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal };
                p1.Children.Add(new TextBlock { Text = "σ (сглаживание):", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _nlmSigmaSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 5, Maximum = 40, Value = 15, Width = 100 };
                p1.Children.Add(_nlmSigmaSlider);
                var nlmVal = new TextBlock { Text = "15", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 28 };
                _nlmSigmaSlider.ValueChanged += (_, __) => nlmVal.Text = ((int)_nlmSigmaSlider.Value).ToString();
                p1.Children.Add(nlmVal);
                ParametersStack.Children.Add(p1);
            }
            else if (methodId == "Deblur")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal };
                p1.Children.Add(new TextBlock { Text = "Итерации:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _deblurIterationsSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 1, Maximum = 8, Value = 3, Width = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
                p1.Children.Add(_deblurIterationsSlider);
                var debVal = new TextBlock { Text = "3", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 24 };
                _deblurIterationsSlider.ValueChanged += (_, __) => debVal.Text = ((int)_deblurIterationsSlider.Value).ToString();
                p1.Children.Add(debVal);
                ParametersStack.Children.Add(p1);
            }
            else if (methodId == "SeamCarving")
            {
                int maxCols = _sourceBitmap != null ? Math.Max(1, _sourceBitmap.Width - 10) : 100;
                int maxRows = _sourceBitmap != null ? Math.Max(1, _sourceBitmap.Height - 10) : 100;
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Удалить столбцов:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _seamCarvingColsSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 0, Maximum = maxCols, Value = Math.Min(20, maxCols), Width = 120, TickFrequency = 1, IsSnapToTickEnabled = true };
                p1.Children.Add(_seamCarvingColsSlider);
                var v1 = new TextBlock { Text = ((int)(_seamCarvingColsSlider.Value)).ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 28 };
                _seamCarvingColsSlider.ValueChanged += (_, __) => v1.Text = ((int)_seamCarvingColsSlider.Value).ToString();
                p1.Children.Add(v1);
                ParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Удалить строк:", Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _seamCarvingRowsSlider = new Slider { Style = (Style)FindResource("DarkSlider"), Minimum = 0, Maximum = maxRows, Value = Math.Min(20, maxRows), Width = 120, TickFrequency = 1, IsSnapToTickEnabled = true };
                p2.Children.Add(_seamCarvingRowsSlider);
                var v2 = new TextBlock { Text = ((int)(_seamCarvingRowsSlider.Value)).ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"), MinWidth = 28 };
                _seamCarvingRowsSlider.ValueChanged += (_, __) => v2.Text = ((int)_seamCarvingRowsSlider.Value).ToString();
                p2.Children.Add(v2);
                ParametersStack.Children.Add(p2);
            }
            else if (Array.IndexOf(AvailableIds, methodId) < 0)
            {
                ParametersPanel.Visibility = Visibility.Collapsed;
                var msg = new TextBlock
                {
                    Text = "Этот метод пока в разработке.",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush"),
                    Margin = new Thickness(0, 8, 0, 0)
                };
                ParametersStack.Children.Add(msg);
            }
        }

        private void LoadSource_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
            if (ofd.ShowDialog() != true) return;

            _sourceBitmap?.Dispose();
            _sourceBitmap = new Bitmap(ofd.FileName);
            SourceImageControl.Source = MainWindow.BitmapToImageSource(_sourceBitmap);
            SourceCaptionText.Text = "Загруженный файл";
            SourceSizeText.Text = $"{_sourceBitmap.Width} × {_sourceBitmap.Height} px";
            LoadSourceButton.Content = "Загрузить другое";

            if (Owner is MainWindow main)
                main.SetOriginalImage((Bitmap)_sourceBitmap.Clone());
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_sourceBitmap == null)
            {
                MessageBox.Show("Сначала загрузите изображение.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Array.IndexOf(AvailableIds, _methodId) < 0)
            {
                MessageBox.Show("Этот метод ещё не реализован.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _resultBitmap?.Dispose();
                _resultBitmap = _methodId switch
                {
                    "LinearContrast" => ImageProcessors.LinearContrastStretch(_sourceBitmap),
                    "HistogramEq" => ImageProcessors.HistogramEqualization(_sourceBitmap),
                    "MedianFilter" => ImageProcessors.MedianFilter(_sourceBitmap, radius: 1),
                    "WienerFilter" => ApplyWiener(),
                    "Gamma" => ApplyGamma(),
                    "UnsharpMask" => ApplyUnsharpMask(),
                    "BilateralFilter" => ApplyBilateral(),
                    "Morphology" => ApplyMorphology(),
                    "EdgeDetection" => ApplyCanny(),
                    "DenoiseNLM" => ApplyNLM(),
                    "Deblur" => ApplyDeblur(),
                    "SeamCarving" => ApplySeamCarving(),
                    _ => null
                };

                if (_resultBitmap != null)
                {
                    ResultPlaceholderText.Visibility = Visibility.Collapsed;
                    ResultImageControl.Source = MainWindow.BitmapToImageSource(_resultBitmap);
                    ResultImageControl.Opacity = 0;
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    ResultImageControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                    if (Owner is MainWindow main)
                        main.SetProcessedImage((Bitmap)_resultBitmap.Clone());

                    AnimateResultPanelHighlight();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AnimateResultPanelHighlight()
        {
            var glow = new DropShadowEffect
            {
                Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6366F1"),
                BlurRadius = 22,
                ShadowDepth = 0,
                Opacity = 0.4
            };
            ResultImageBorder.Effect = glow;
            var anim = new DoubleAnimation(0.4, 0, TimeSpan.FromMilliseconds(650))
            {
                BeginTime = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (_, _) => ResultImageBorder.Effect = null;
            glow.BeginAnimation(DropShadowEffect.OpacityProperty, anim);
        }

        private Bitmap ApplyWiener()
        {
            int radius = _wienerRadiusSlider != null ? (int)_wienerRadiusSlider.Value : 1;
            string text = (_wienerNoiseTextBox?.Text ?? "10").Replace(',', '.');
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double noiseVar))
                noiseVar = 10;
            return ImageProcessors.WienerFilter(_sourceBitmap!, windowRadius: radius, noiseVariance: noiseVar);
        }

        private Bitmap ApplyGamma()
        {
            double gamma = _gammaSlider != null ? _gammaSlider.Value : 2.2;
            return ImageProcessors.GammaCorrection(_sourceBitmap!, gamma: gamma, c: 1.0);
        }

        private Bitmap ApplyUnsharpMask()
        {
            double strength = _unsharpStrengthSlider != null ? _unsharpStrengthSlider.Value : 1.5;
            int radius = _unsharpRadiusSlider != null ? (int)_unsharpRadiusSlider.Value : 1;
            return ImageProcessors.UnsharpMask(_sourceBitmap!, strength: strength, blurRadius: radius);
        }

        private Bitmap ApplyBilateral()
        {
            int r = _bilateralRadiusSlider != null ? (int)_bilateralRadiusSlider.Value : 2;
            double sigmaR = _bilateralSigmaRSlider != null ? _bilateralSigmaRSlider.Value : 30;
            return ImageProcessors.BilateralFilter(_sourceBitmap!, radius: r, sigmaSpace: 2, sigmaRange: sigmaR);
        }

        private Bitmap ApplyMorphology()
        {
            int r = _morphologyRadiusSlider != null ? (int)_morphologyRadiusSlider.Value : 1;
            bool isOpen = _morphologyCombo == null || _morphologyCombo.SelectedIndex == 0;
            return isOpen ? ImageProcessors.MorphologyOpen(_sourceBitmap!, r) : ImageProcessors.MorphologyClose(_sourceBitmap!, r);
        }

        private Bitmap ApplyCanny()
        {
            double low = _cannyLowSlider != null ? _cannyLowSlider.Value : 0.15;
            double high = _cannyHighSlider != null ? _cannyHighSlider.Value : 0.35;
            return ImageProcessors.CannyEdgeDetection(_sourceBitmap!, lowThreshold: low, highThreshold: high);
        }

        private Bitmap ApplyNLM()
        {
            double sigma = _nlmSigmaSlider != null ? _nlmSigmaSlider.Value : 15;
            return ImageProcessors.DenoiseNLM(_sourceBitmap!, searchRadius: 3, patchRadius: 1, sigma: sigma);
        }

        private Bitmap ApplyDeblur()
        {
            int iter = _deblurIterationsSlider != null ? (int)_deblurIterationsSlider.Value : 3;
            return ImageProcessors.Deblur(_sourceBitmap!, iterations: iter);
        }

        private Bitmap ApplySeamCarving()
        {
            int cols = _seamCarvingColsSlider != null ? (int)_seamCarvingColsSlider.Value : 0;
            int rows = _seamCarvingRowsSlider != null ? (int)_seamCarvingRowsSlider.Value : 0;
            int w = _sourceBitmap!.Width;
            int h = _sourceBitmap.Height;
            cols = Math.Clamp(cols, 0, w - 1);
            rows = Math.Clamp(rows, 0, h - 1);
            return ImageProcessors.SeamCarving(_sourceBitmap, removeColumns: cols, removeRows: rows);
        }

        private void SaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (_resultBitmap == null)
            {
                MessageBox.Show("Сначала примените метод.", "Сохранение",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp",
                FileName = "output.png"
            };
            if (sfd.ShowDialog() != true) return;

            string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
            var format = System.Drawing.Imaging.ImageFormat.Png;
            if (ext == ".jpg" || ext == ".jpeg") format = System.Drawing.Imaging.ImageFormat.Jpeg;
            else if (ext == ".bmp") format = System.Drawing.Imaging.ImageFormat.Bmp;

            _resultBitmap.Save(sfd.FileName, format);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
