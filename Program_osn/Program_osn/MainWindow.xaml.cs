using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ImageEnhancement;

namespace ImageEnhancementWpf
{
    public partial class MainWindow : Window
    {
        private Bitmap? _originalBitmap;
        private Bitmap? _processedBitmap;

        private const double ZoomMin = 0.25;
        private const double ZoomMax = 4.0;
        private const double ZoomStep = 1.15;
        private double _originalZoomScale = 1.0;
        private double _processedZoomScale = 1.0;
        private double _compareZoomScale = 1.0;

        private bool _originalPanning;
        private System.Windows.Point _originalPanStart;
        private double _originalScrollStartX, _originalScrollStartY;
        private bool _processedPanning;
        private System.Windows.Point _processedPanStart;
        private double _processedScrollStartX, _processedScrollStartY;
        private bool _comparePanning;
        private System.Windows.Point _comparePanStart;
        private double _compareScrollStartX, _compareScrollStartY;

        private System.Windows.Threading.DispatcherTimer? _methodPanelHideTimer;
        private bool _viewTabAfter; // false = ДО, true = ПОСЛЕ
        private bool _viewCompare;  // true = режим сравнения
        private double _compareContainerWidth = 400;
        private double _compareContainerHeight = 300;
        private bool _compareDividerDragging;
        private string? _selectedMethodId;
        private string? _selectedMethodName;
        private string? _selectedMethodIcon;
        private MethodItem? _selectedMethodItem;
        private string? _currentImageFileName;
        private string? _currentImageFilePath;
        private string? _currentFormula;
        private readonly List<string> _pipelineSteps = new();
        private readonly List<string> _pipelineFormulas = new();
        private readonly Stack<(Bitmap? bitmap, List<string> steps, List<string> formulas)> _undoStack = new();

        // Контролы параметров методов (как в MethodWindow)
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
        private ComboBox? _pseudoColorCombo;

        private static readonly string[] AvailableMethodIds = {
            "LinearContrast", "HistogramEq", "MedianFilter", "WienerFilter", "Gamma", "UnsharpMask",
            "BilateralFilter", "Morphology", "EdgeDetection", "DenoiseNLM", "Deblur", "SeamCarving",
            "ESRGAN", "CLAHE", "MSRetinex", "LogTransform", "OtsuBinarize", "PseudoColor"
        };

        /// <summary> Параметры методов, считанные с UI перед фоновой обработкой. </summary>
        private sealed class ApplyParams
        {
            public int WienerRadius = 1;
            public string WienerNoiseText = "10";
            public double Gamma = 2.2;
            public double UnsharpStrength = 1.5;
            public int UnsharpRadius = 1;
            public int BilateralRadius = 2;
            public double BilateralSigmaR = 30;
            public int MorphologyOpenIndex = 0;
            public int MorphologyRadius = 1;
            public double CannyLow = 0.15;
            public double CannyHigh = 0.35;
            public double NlmSigma = 15;
            public int DeblurIterations = 3;
            public int SeamCarvingCols = 0;
            public int SeamCarvingRows = 0;
            public int PseudoColorMap = 0;
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadMethodCards();
            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
            ThemeManager.ThemeChanged += OnThemeChanged;
            Closed += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            RefreshTabBindings();
            if (!string.IsNullOrEmpty(_currentFormula))
                BuildFormulaPanel(MethodDetailFormulaPanel, _currentFormula);
        }

        // ──────────────────────────────────────────────────────────────────
        #region Drag & Drop (window-level)

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DragOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            // Скрываем оверлей только когда мышь ушла за пределы окна
            var pos = e.GetPosition(this);
            if (pos.X <= 0 || pos.Y <= 0 || pos.X >= ActualWidth || pos.Y >= ActualHeight)
                DragOverlay.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    LoadImageFromFile(files[0]);
            }
            e.Handled = true;
        }

        private void DragOverlay_MouseLeave(object sender, MouseEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void RefreshTabBindings()
        {
            string activeBg   = "TabActiveBrush";
            string inactiveBg = "TabInactiveBrush";
            string activeText = "TextSecondary2Brush";
            string mutedText  = "TextMutedBrush";

            if (_viewCompare)
            {
                TabBefore.SetResourceReference(Border.BackgroundProperty, inactiveBg);
                TabAfter.SetResourceReference(Border.BackgroundProperty,  inactiveBg);
                TabCompare.SetResourceReference(Border.BackgroundProperty, activeBg);
                if (TabBefore.Child is StackPanel sp1 && sp1.Children[0] is TextBlock tb1) tb1.SetResourceReference(TextBlock.ForegroundProperty, mutedText);
                if (TabAfter.Child  is StackPanel sp2 && sp2.Children[0] is TextBlock tb2) tb2.SetResourceReference(TextBlock.ForegroundProperty, mutedText);
                TabCompareText.SetResourceReference(TextBlock.ForegroundProperty, activeText);
                TabBeforeBar.Visibility  = Visibility.Collapsed;
                TabAfterBar.Visibility   = Visibility.Collapsed;
                TabCompareBar.Visibility = Visibility.Visible;
            }
            else if (_viewTabAfter)
            {
                TabBefore.SetResourceReference(Border.BackgroundProperty, inactiveBg);
                TabAfter.SetResourceReference(Border.BackgroundProperty,  activeBg);
                TabCompare.SetResourceReference(Border.BackgroundProperty, inactiveBg);
                if (TabBefore.Child is StackPanel sp1 && sp1.Children[0] is TextBlock tb1) tb1.SetResourceReference(TextBlock.ForegroundProperty, mutedText);
                if (TabAfter.Child  is StackPanel sp2 && sp2.Children[0] is TextBlock tb2) tb2.SetResourceReference(TextBlock.ForegroundProperty, activeText);
                TabCompareText.SetResourceReference(TextBlock.ForegroundProperty, mutedText);
                TabBeforeBar.Visibility  = Visibility.Collapsed;
                TabAfterBar.Visibility   = Visibility.Visible;
                TabCompareBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                TabBefore.SetResourceReference(Border.BackgroundProperty, activeBg);
                TabAfter.SetResourceReference(Border.BackgroundProperty,  inactiveBg);
                TabCompare.SetResourceReference(Border.BackgroundProperty, inactiveBg);
                if (TabBefore.Child is StackPanel sp1 && sp1.Children[0] is TextBlock tb1) tb1.SetResourceReference(TextBlock.ForegroundProperty, activeText);
                if (TabAfter.Child  is StackPanel sp2 && sp2.Children[0] is TextBlock tb2) tb2.SetResourceReference(TextBlock.ForegroundProperty, mutedText);
                TabCompareText.SetResourceReference(TextBlock.ForegroundProperty, mutedText);
                TabBeforeBar.Visibility  = Visibility.Visible;
                TabAfterBar.Visibility   = Visibility.Collapsed;
                TabCompareBar.Visibility = Visibility.Collapsed;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "upload.ico");
            if (File.Exists(path))
            {
                try
                {
                    var uri = new Uri(path, UriKind.Absolute);
                    UploadIconImage.Source = BitmapFrame.Create(uri);
                }
                catch { /* иконка опциональна */ }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (ctrl && e.Key == Key.O)
            {
                LoadImage_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.S)
            {
                SaveImage_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.R)
            {
                MethodReset_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.H)
            {
                OpenHistoryWindow();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.D1)
            {
                TabBefore_Click(sender, null!);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.D2)
            {
                TabAfter_Click(sender, null!);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.D3)
            {
                TabCompare_Click(sender, null!);
                e.Handled = true;
            }
            else if (ctrl && (e.Key == Key.OemPlus || e.Key == Key.Add))
            {
                ZoomIn_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrl && (e.Key == Key.OemMinus || e.Key == Key.Subtract))
            {
                ZoomOut_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                FitZoom_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F1)
            {
                new HelpWindow { Owner = this }.ShowDialog();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.K)
            {
                MethodChainApply_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.V)
            {
                PasteFromClipboard();
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.E)
            {
                ExportReport_Click(sender, e);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.Z)
            {
                UndoLastStep();
                e.Handled = true;
            }
        }

        private void PasteFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var bitmapSource = Clipboard.GetImage();
                    if (bitmapSource == null) return;
                    // BitmapSource → System.Drawing.Bitmap через PNG round-trip
                    Bitmap bmp;
                    using (var ms = new MemoryStream())
                    {
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
                        encoder.Save(ms);
                        ms.Position = 0;
                        bmp = new Bitmap(ms);
                    }
                    _currentImageFilePath = null;
                    _currentImageFileName = "clipboard.png";
                    _processedBitmap?.Dispose();
                    _processedBitmap = null;
                    _originalBitmap?.Dispose();
                    _originalBitmap = bmp;
                    OriginalImageControl.Source = BitmapToImageSource(_originalBitmap);
                    LoadZoneBorder.Visibility = Visibility.Collapsed;
                    ProcessedImageControl.Source = null;
                    ProcessedPlaceholder.Visibility = Visibility.Visible;
                    if (_viewCompare) { _viewCompare = false; ComparePanel.Visibility = Visibility.Collapsed; OriginalPanel.Visibility = Visibility.Visible; }
                    ResetZoom();
                    UpdateCompareTabVisibility();
                    UpdateImageInfoPanel();
                    _pipelineSteps.Clear();
                    _pipelineFormulas.Clear();
                    UpdatePipelineDisplay();
                    UpdateChainButton();
                    Title = "ImageUP — изображение из буфера обмена";
                }
                else if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (string? f in files)
                    {
                        if (f == null) continue;
                        var ext = Path.GetExtension(f).ToLowerInvariant();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                        {
                            LoadImageFromFile(f);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось вставить из буфера: {ex.Message}", "Вставка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenHistoryWindow()
        {
            var w = new HistoryWindow { Owner = this };
            w.ShowDialog();
        }

        private void LoadMethodCards()
        {
            // ── Формулы — прямые цитаты из Гонсалеса–Вудса (3-е изд.) ──────
            var methods = new List<MethodItem>
            {
                // ── Контраст и яркость ──────────────────────────────────────
                new MethodItem {
                    Id = "LinearContrast", Icon = "▤",
                    Category = "Контраст и яркость",
                    Name = "Линейное контрастирование",
                    Description = "Растяжение гистограммы яркостей в полный диапазон [0; 255].",
                    // Г&В §3.2.4 кусочно-линейное растяжение; частный случай — полное растяжение
                    Formula = "g = (f − f_min) / (f_max − f_min) × 255",
                    Principle = "По изображению находятся минимальная и максимальная яркость. Каждый пиксель линейно масштабируется в [0, 255]. Усиливает контраст тусклых снимков — рентген, CCTV, слабо освещённые сцены."
                },
                new MethodItem {
                    Id = "HistogramEq", Icon = "📊",
                    Category = "Контраст и яркость",
                    Name = "Эквализация гистограммы",
                    Description = "Перераспределение яркостей для выравнивания гистограммы.",
                    // Г&В §3.3.1, формула (3.3-8)
                    Formula = "s_k = T(r_k) = (L−1) · Σ_{j=0}^{k} p_r(r_j)",
                    Principle = "Строится накопленная функция распределения (CDF) яркостей. Каждая яркость r заменяется на значение s, пропорциональное её накопленной доле. Детали в тенях и светах проявляются лучше. Применяется в медицине (рентген, МРТ), криминалистике."
                },
                new MethodItem {
                    Id = "CLAHE", Icon = "⊞",
                    Category = "Контраст и яркость",
                    Name = "CLAHE",
                    Description = "Адаптивная эквализация с ограничением контраста по тайлам.",
                    // Г&В §3.3.3 локальная гистограммная обработка; CLAHE — расширение
                    Formula = "H_clip(i) = min(H(i), T_clip)\nT_clip = N_tile · L / 256",
                    Principle = "Изображение делится на тайлы. В каждом тайле строится гистограмма с ограничением пиков (clipLimit); избыток перераспределяется равномерно. Результаты интерполируются — проявляются локальные детали без пересветки. Стандарт в медицинской визуализации: рентген, МРТ, КТ.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "Gamma", Icon = "γ",
                    Category = "Контраст и яркость",
                    Name = "Гамма-коррекция",
                    Description = "Нелинейная коррекция яркости по степенной функции.",
                    // Г&В §3.2.3, формула (3.2-3)
                    Formula = "s = c · r^γ,   r ∈ [0,1]\nγ < 1 → светлее,  γ > 1 → темнее",
                    Principle = "Яркость возводится в степень γ: при γ < 1 изображение светлеет, при γ > 1 — темнеет. Параметр c — масштаб. Широко используется в медицине, видеонаблюдении и при калибровке дисплеев.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "LogTransform", Icon = "ln",
                    Category = "Контраст и яркость",
                    Name = "Логарифмическое преобразование",
                    Description = "Расширяет тёмные области, сжимает светлые — детали в тенях.",
                    // Г&В §3.2.2, формула (3.2-2)
                    Formula = "s = c · log(1 + r)\nc = 255 / log(256)",
                    Principle = "Логарифм сжимает большие значения яркости и растягивает малые. Идеален для изображений с широким динамическим диапазоном: рентгеновские снимки, изображения с Фурье-спектром, астрофотография.",
                    IsAvailable = true
                },

                // ── Шумоподавление и фильтрация ────────────────────────────
                new MethodItem {
                    Id = "MedianFilter", Icon = "◇",
                    Category = "Шумоподавление",
                    Name = "Медианный фильтр",
                    Description = "Подавление импульсного шума заменой пикселя медианой по окрестности.",
                    // Г&В §3.5.2, §5.3.2
                    Formula = "g(x,y) = median{ f(x+s, y+t) : (s,t) ∈ S_xy }",
                    Principle = "В скользящем окне выбирается медиана яркостей. Эффективно убирает шум «соль и перец», сохраняя границы объектов лучше, чем линейное сглаживание."
                },
                new MethodItem {
                    Id = "BilateralFilter", Icon = "◯",
                    Category = "Шумоподавление",
                    Name = "Двусторонний фильтр",
                    Description = "Сглаживание с сохранением границ.",
                    // Г&В §5.3.3 адаптивный медианный; билатеральный — Tomasi & Manduchi 1998
                    Formula = "g(p) = Σ_q f(q)·e^(−‖p−q‖²/2σ_s²)·e^(−|f(p)−f(q)|²/2σ_r²)",
                    Principle = "Веса усреднения зависят и от пространственного расстояния (σ_s), и от разности яркостей (σ_r). Пиксели за контуром почти не влияют — граница остаётся чёткой.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "DenoiseNLM", Icon = "≋",
                    Category = "Шумоподавление",
                    Name = "Шумоподавление (NLM)",
                    Description = "Нелокальное усреднение по похожим патчам.",
                    // Buades et al. 2005; принцип из Г&В §5.3.3
                    Formula = "NL[f](x) = Σ_y w(x,y)·f(y)\nw(x,y) ∝ exp(−‖P_x−P_y‖² / h²)",
                    Principle = "Для каждого пикселя ищутся похожие блоки (патчи) по всему изображению. Усреднение с весами по схожести блоков даёт лучшее соотношение сигнал/шум без потери деталей.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "WienerFilter", Icon = "⚙",
                    Category = "Шумоподавление",
                    Name = "Фильтр Винера",
                    Description = "Оптимальное шумоподавление с восстановлением в частотной области.",
                    // Г&В §5.8, формула (5.8-4)
                    Formula = "Ŵ(u,v) = [H*(u,v)·S_f] / [|H|²·S_f + S_n]",
                    Principle = "Оптимальный линейный фильтр в смысле минимума среднеквадратической ошибки. Ослабляет частоты с плохим SNR и восстанавливает остальные. Применяется при восстановлении размытых и зашумлённых снимков."
                },

                // ── Резкость и восстановление ──────────────────────────────
                new MethodItem {
                    Id = "UnsharpMask", Icon = "✴",
                    Category = "Резкость",
                    Name = "Нерезкое маскирование (USM)",
                    Description = "Усиление контуров за счёт вычитания размытой копии.",
                    // Г&В §3.6.3, формула (3.6-10)
                    Formula = "g(x,y) = f(x,y) + k·[f(x,y) − f̄(x,y)],  k > 0",
                    Principle = "Размытая версия изображения вычитается из оригинала — получается «маска контуров». При сложении с оригиналом (к > 0) контуры усиливаются. При k = 1 — нерезкое маскирование, k > 1 — «подъём высоких частот».",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "Deblur", Icon = "🎯",
                    Category = "Резкость",
                    Name = "Деконволюция (Ричардсон–Люси)",
                    Description = "Итеративное восстановление резкости размытого изображения.",
                    // Richardson 1972, Lucy 1974; Г&В §5.7, §5.8
                    Formula = "f_{k+1} = f_k · [h(-x,-y) ⊛ (g / (h ⊛ f_k))]",
                    Principle = "EM-алгоритм байесовского восстановления. На каждой итерации к оценке изображения применяется поправочный коэффициент, вычисленный через свёртку с ядром размытия.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "MSRetinex", Icon = "🌅",
                    Category = "Резкость",
                    Name = "Мультимасштабный ретинекс (MSR)",
                    Description = "Коррекция неравномерного освещения — имитация адаптации зрения.",
                    // Land & McCann 1971; Г&В §3.4 и §4.9.6 (гомоморфная фильтрация)
                    Formula = "R(x,y) = Σ_s w_s · [log I(x,y) − log(G_σs ∗ I)(x,y)]",
                    Principle = "Логарифмическое пространство позволяет разделить освещённость и отражательную способность. На нескольких масштабах вычисляется разность логарифмов оригинала и размытого изображения. Результат — нормализованный по освещению снимок.",
                    IsAvailable = true
                },

                // ── Сегментация и анализ ────────────────────────────────────
                new MethodItem {
                    Id = "EdgeDetection", Icon = "✂",
                    Category = "Сегментация",
                    Name = "Детектор Кэнни",
                    Description = "Выделение границ объектов по градиенту с гистерезисным порогом.",
                    // Г&В §10.2.6, Canny 1986
                    Formula = "M(x,y) = √(G_x² + G_y²),  α = arctan(G_y/G_x)\nT_low < M(x,y) < T_high (гистерезис)",
                    Principle = "Этапы: размытие Гауссом, расчёт градиента, подавление немаксимумов, пороговая обработка с гистерезисом. Даёт тонкие и связные контуры с минимумом ложных срабатываний.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "Morphology", Icon = "▢",
                    Category = "Сегментация",
                    Name = "Морфологические операции",
                    Description = "Эрозия, дилатация, открытие и закрытие.",
                    // Г&В §9.2, формулы (9.2-1) и (9.2-4)
                    Formula = "Эрозия:   (A ⊖ B)(x) = min_{b∈B} A(x+b)\nДилатация: (A ⊕ B)(x) = max_{b∈B} A(x−b)",
                    Principle = "Структурный элемент B «прокатывается» по изображению. Открытие (эрозия + дилатация) убирает мелкие объекты; закрытие (дилатация + эрозия) заполняет разрывы. Применяется для анализа формы объектов.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "OtsuBinarize", Icon = "◑",
                    Category = "Сегментация",
                    Name = "Бинаризация по методу Оцу",
                    Description = "Автоматический выбор порога бинаризации по максимуму межклассовой дисперсии.",
                    // Г&В §10.3.3, формула (10.3-14); Otsu 1979
                    Formula = "T* = arg max σ²_B(T)\nσ²_B = ω₀·ω₁·(μ₀−μ₁)²",
                    Principle = "Гистограмма делится порогом T на два класса (фон и объект). Метод Оцу выбирает T, при котором межклассовая дисперсия σ²_B максимальна — пиксели двух классов максимально различаются по яркости. Применяется в сегментации медицинских снимков, OCR, анализе документов.",
                    IsAvailable = true
                },

                // ── Специальные ─────────────────────────────────────────────
                new MethodItem {
                    Id = "SeamCarving", Icon = "↔",
                    Category = "Специальные",
                    Name = "Seam Carving",
                    Description = "Изменение размера с сохранением содержания через удаление «швов».",
                    // Avidan & Shamir, SIGGRAPH 2007; принцип энергии из Г&В §10.2.5
                    Formula = "e(x,y) = |∂I/∂x| + |∂I/∂y|\nseam* = arg min_s Σ_i e(s_i, i)",
                    Principle = "Строится карта энергии (градиент яркости). Шов — минимально-энергетический связный путь через всё изображение. Динамическое программирование находит оптимальный шов. Удаление/добавление швов изменяет размер без искажения ключевых объектов.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "PseudoColor", Icon = "🎨",
                    Category = "Специальные",
                    Name = "Псевдоцвет (тепловая карта)",
                    Description = "Преобразование яркостей полутонового изображения в цветовую шкалу.",
                    // Г&В §6.3.2, §6.3.1
                    Formula = "f_R(x,y) = T_R[f(x,y)]\nf_G(x,y) = T_G[f(x,y)]\nf_B(x,y) = T_B[f(x,y)]",
                    Principle = "Каждое значение яркости r независимо отображается в три цветовых канала через функции T_R, T_G, T_B. В режиме Jet (классическая тепловая): синий→зелёный→красный. Применяется в медицинской визуализации (термографии, МРТ), научных данных и дистанционном зондировании.",
                    IsAvailable = true
                },
                new MethodItem {
                    Id = "ESRGAN", Icon = "✨",
                    Category = "Специальные",
                    Name = "Real-ESRGAN (супер-разрешение ×4)",
                    Description = "Нейросетевое увеличение разрешения в 4 раза с генерацией деталей.",
                    // Wang et al., ECCV 2018 / 2021
                    Formula = "I_HR = G_θ(I_LR),   ×4 масштаб\nG_θ — RRDB-сеть с перцептивными и состязательными потерями",
                    Principle = "Нейросетевая модель Real-ESRGAN (ONNX) генерирует высококачественные детали при увеличении. Требует файл модели Real-ESRGAN-General-x4v3.onnx в папке Models/.",
                    IsAvailable = true
                }
            };

            // Цвета категорий (индикатор и акцент иконки)
            var categoryBarColors = new Dictionary<string, string>
            {
                ["Контраст и яркость"] = "#F59E0B",
                ["Шумоподавление"]     = "#3B82F6",
                ["Резкость"]           = "#8B5CF6",
                ["Сегментация"]        = "#10B981",
                ["Специальные"]        = "#EC4899",
            };
            var categoryAccentColors = new Dictionary<string, string>
            {
                ["Контраст и яркость"] = "#D97706",
                ["Шумоподавление"]     = "#2563EB",
                ["Резкость"]           = "#7C3AED",
                ["Сегментация"]        = "#059669",
                ["Специальные"]        = "#DB2777",
            };

            var groups = new List<MethodGroup>();
            var categories = new Dictionary<string, MethodGroup>();
            foreach (var m in methods)
            {
                var cat = m.Category ?? "Прочее";
                if (!categories.TryGetValue(cat, out var group))
                {
                    string barHex = categoryBarColors.TryGetValue(cat, out var bh) ? bh : "#6366F1";
                    var barBrush = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(barHex));
                    group = new MethodGroup { CategoryName = cat, CategoryColor = barBrush };
                    categories[cat] = group;
                    groups.Add(group);
                }
                string accHex = categoryAccentColors.TryGetValue(cat, out var ah) ? ah : "#4F46E5";
                m.CategoryAccentBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accHex));
                group.Items.Add(m);
            }
            MethodGroupsControl.ItemsSource = groups;
        }

        private void MethodListItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.DataContext is not MethodItem item)
                return;
            if (_selectedMethodItem != null) _selectedMethodItem.IsSelected = false;
            item.IsSelected = true;
            _selectedMethodItem = item;
            _selectedMethodId   = item.Id;
            _selectedMethodName = item.Name;
            _selectedMethodIcon = item.Icon;
            _currentFormula     = string.IsNullOrEmpty(item.Formula) ? "—" : item.Formula;
            UpdateChainButton();
            MethodDetailTitle.Text = item.Name;
            BuildFormulaPanel(MethodDetailFormulaPanel, _currentFormula);
            MethodDetailPrinciple.Text = string.IsNullOrEmpty(item.Principle) ? "—" : item.Principle;
            BuildMethodParameters(item.Id);
        }

        private void TabBefore_Click(object sender, MouseButtonEventArgs e)
        {
            _viewTabAfter = false;
            _viewCompare  = false;
            OriginalPanel.Visibility  = Visibility.Visible;
            ProcessedPanel.Visibility = Visibility.Collapsed;
            ComparePanel.Visibility   = Visibility.Collapsed;
            RefreshTabBindings();
            UpdateCenterZoomText();
            UpdateImageInfoPanel();
        }

        private void TabAfter_Click(object sender, MouseButtonEventArgs e)
        {
            _viewTabAfter = true;
            _viewCompare  = false;
            OriginalPanel.Visibility  = Visibility.Collapsed;
            ProcessedPanel.Visibility = Visibility.Visible;
            ComparePanel.Visibility   = Visibility.Collapsed;
            RefreshTabBindings();
            UpdateCenterZoomText();
            UpdateImageInfoPanel();
        }

        private void TabCompare_Click(object sender, MouseButtonEventArgs e)
        {
            if (_originalBitmap == null || _processedBitmap == null) return;
            _viewTabAfter = false;
            _viewCompare  = true;
            _compareZoomScale = 1.0;
            CompareImageScale.ScaleX = CompareImageScale.ScaleY = 1.0;
            CompareScrollViewer.ScrollToHorizontalOffset(0);
            CompareScrollViewer.ScrollToVerticalOffset(0);
            OriginalPanel.Visibility  = Visibility.Collapsed;
            ProcessedPanel.Visibility = Visibility.Collapsed;
            ComparePanel.Visibility   = Visibility.Visible;
            CompareOriginalImage.Source  = OriginalImageControl.Source;
            CompareProcessedImage.Source = ProcessedImageControl.Source;
            CompareImageContainer.Width  = _originalBitmap!.Width;
            CompareImageContainer.Height = _originalBitmap.Height;
            CompareSlider.Value = 50;
            RefreshTabBindings();
            UpdateCenterZoomText();
            UpdateImageInfoPanel();
        }

        private void UpdateCenterZoomText()
        {
            double scale = _viewCompare ? _compareZoomScale : (_viewTabAfter ? _processedZoomScale : _originalZoomScale);
            CenterZoomText.Text = $"{(int)(scale * 100)}%";
        }

        private void UpdateCompareTabVisibility()
        {
            TabCompare.Visibility = (_originalBitmap != null && _processedBitmap != null) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CompareContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _compareContainerWidth = e.NewSize.Width;
            _compareContainerHeight = e.NewSize.Height;
            UpdateCompareSplit((int)CompareSlider.Value);
        }

        private void CompareSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_compareDividerDragging)
                UpdateCompareSplit((int)e.NewValue);
        }

        private void UpdateCompareSplit(int percent)
        {
            if (_compareContainerWidth <= 0) return;
            double splitX = (percent / 100.0) * _compareContainerWidth;
            CompareProcessedClip.Rect = new Rect(0, 0, splitX, _compareContainerHeight);
            CompareDivider.Margin = new Thickness(Math.Max(0, splitX - 1), 0, 0, 0);
        }

        private void CompareDivider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _compareDividerDragging = true;
            CompareDivider.CaptureMouse();
            e.Handled = true;
        }

        private void CompareDivider_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_compareDividerDragging) return;
            var pos = e.GetPosition(CompareImageContainer);
            int percent = (int)Math.Clamp(pos.X / _compareContainerWidth * 100, 0, 100);
            CompareSlider.Value = percent;
            UpdateCompareSplit(percent);
        }

        private void CompareDivider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _compareDividerDragging = false;
            CompareDivider.ReleaseMouseCapture();
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_viewCompare)
                ApplyZoomCompare(-1, default);
            else if (_viewTabAfter)
                ApplyZoomProcessed(-1, default);
            else
                ApplyZoomOriginal(-1, default);
            UpdateCenterZoomText();
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_viewCompare)
                ApplyZoomCompare(1, default);
            else if (_viewTabAfter)
                ApplyZoomProcessed(1, default);
            else
                ApplyZoomOriginal(1, default);
            UpdateCenterZoomText();
        }

        private void FitZoom_Click(object sender, RoutedEventArgs e)
        {
            if (_viewCompare)
            {
                _compareZoomScale = 1.0;
                CompareImageScale.ScaleX = CompareImageScale.ScaleY = 1.0;
                CompareScrollViewer.ScrollToHorizontalOffset(0);
                CompareScrollViewer.ScrollToVerticalOffset(0);
            }
            else if (_viewTabAfter)
            {
                _processedZoomScale = 1.0;
                ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = 1.0;
                ProcessedScrollViewer.ScrollToHorizontalOffset(0);
                ProcessedScrollViewer.ScrollToVerticalOffset(0);
            }
            else
            {
                _originalZoomScale = 1.0;
                OriginalImageScale.ScaleX = OriginalImageScale.ScaleY = 1.0;
                OriginalScrollViewer.ScrollToHorizontalOffset(0);
                OriginalScrollViewer.ScrollToVerticalOffset(0);
            }
            UpdateZoomLabels();
            UpdateCenterZoomText();
        }

        private void BuildMethodParameters(string methodId)
        {
            MethodParametersStack.Children.Clear();
            _wienerRadiusSlider = null; _wienerNoiseTextBox = null; _gammaSlider = null;
            _unsharpStrengthSlider = _unsharpRadiusSlider = null;
            _bilateralRadiusSlider = _bilateralSigmaRSlider = null;
            _morphologyCombo = null; _morphologyRadiusSlider = null;
            _cannyLowSlider = _cannyHighSlider = null;
            _nlmSigmaSlider = _deblurIterationsSlider = null;
            _seamCarvingColsSlider = _seamCarvingRowsSlider = null;
            _pseudoColorCombo = null;

            var textPrimary = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");
            var textMuted = (System.Windows.Media.Brush)FindResource("TextMutedBrush");
            var darkSlider = (Style)FindResource("DarkSlider");
            var darkComboBox = (Style)FindResource("DarkComboBox");
            var darkTextBox = (Style)FindResource("DarkTextBox");

            if (methodId == "WienerFilter")
            {
                var radiusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                radiusPanel.Children.Add(new TextBlock { Text = "Радиус окна:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _wienerRadiusSlider = new Slider { Style = darkSlider, Minimum = 1, Maximum = 3, Value = 1, Width = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
                radiusPanel.Children.Add(_wienerRadiusSlider);
                var radiusVal = new TextBlock { Text = "1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary };
                _wienerRadiusSlider.ValueChanged += (_, __) => radiusVal.Text = ((int)_wienerRadiusSlider.Value).ToString();
                radiusPanel.Children.Add(radiusVal);
                MethodParametersStack.Children.Add(radiusPanel);
                var noisePanel = new StackPanel { Orientation = Orientation.Horizontal };
                noisePanel.Children.Add(new TextBlock { Text = "Дисперсия шума:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _wienerNoiseTextBox = new TextBox { Text = "10", Width = 70, Style = darkTextBox };
                noisePanel.Children.Add(_wienerNoiseTextBox);
                MethodParametersStack.Children.Add(noisePanel);
            }
            else if (methodId == "Gamma")
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                panel.Children.Add(new TextBlock { Text = "γ (гамма):", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _gammaSlider = new Slider { Style = darkSlider, Minimum = 0.3, Maximum = 3, Value = 2.2, Width = 120 };
                panel.Children.Add(_gammaSlider);
                var valTb = new TextBlock { Text = "2.20", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary };
                _gammaSlider.ValueChanged += (_, __) => valTb.Text = _gammaSlider.Value.ToString("F2");
                panel.Children.Add(valTb);
                MethodParametersStack.Children.Add(panel);
            }
            else if (methodId == "UnsharpMask")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Сила k:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _unsharpStrengthSlider = new Slider { Style = darkSlider, Minimum = 0.5, Maximum = 3, Value = 1.5, Width = 100 };
                p1.Children.Add(_unsharpStrengthSlider);
                var v1 = new TextBlock { Text = "1.5", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary };
                _unsharpStrengthSlider.ValueChanged += (_, __) => v1.Text = _unsharpStrengthSlider.Value.ToString("F1");
                p1.Children.Add(v1);
                MethodParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Радиус размытия:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _unsharpRadiusSlider = new Slider { Style = darkSlider, Minimum = 1, Maximum = 3, Value = 1, Width = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
                p2.Children.Add(_unsharpRadiusSlider);
                var v2 = new TextBlock { Text = "1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 24 };
                _unsharpRadiusSlider.ValueChanged += (_, __) => v2.Text = ((int)_unsharpRadiusSlider.Value).ToString();
                p2.Children.Add(v2);
                MethodParametersStack.Children.Add(p2);
            }
            else if (methodId == "BilateralFilter")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Радиус:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _bilateralRadiusSlider = new Slider { Style = darkSlider, Minimum = 1, Maximum = 4, Value = 2, Width = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
                p1.Children.Add(_bilateralRadiusSlider);
                var v1 = new TextBlock { Text = "2", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 20 };
                _bilateralRadiusSlider.ValueChanged += (_, __) => v1.Text = ((int)_bilateralRadiusSlider.Value).ToString();
                p1.Children.Add(v1);
                MethodParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "σ по яркости:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _bilateralSigmaRSlider = new Slider { Style = darkSlider, Minimum = 10, Maximum = 80, Value = 30, Width = 100 };
                p2.Children.Add(_bilateralSigmaRSlider);
                var v2 = new TextBlock { Text = "30", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 28 };
                _bilateralSigmaRSlider.ValueChanged += (_, __) => v2.Text = ((int)_bilateralSigmaRSlider.Value).ToString();
                p2.Children.Add(v2);
                MethodParametersStack.Children.Add(p2);
            }
            else if (methodId == "Morphology")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Операция:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _morphologyCombo = new ComboBox { Width = 120, SelectedIndex = 0, Style = darkComboBox };
                _morphologyCombo.Items.Add("Открытие");
                _morphologyCombo.Items.Add("Закрытие");
                p1.Children.Add(_morphologyCombo);
                MethodParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Радиус:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _morphologyRadiusSlider = new Slider { Style = darkSlider, Minimum = 1, Maximum = 3, Value = 1, Width = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
                p2.Children.Add(_morphologyRadiusSlider);
                var morphVal = new TextBlock { Text = "1", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 24 };
                _morphologyRadiusSlider.ValueChanged += (_, __) => morphVal.Text = ((int)_morphologyRadiusSlider.Value).ToString();
                p2.Children.Add(morphVal);
                MethodParametersStack.Children.Add(p2);
            }
            else if (methodId == "EdgeDetection")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Нижний порог:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _cannyLowSlider = new Slider { Style = darkSlider, Minimum = 0.05, Maximum = 0.4, Value = 0.15, Width = 100 };
                p1.Children.Add(_cannyLowSlider);
                var lowVal = new TextBlock { Text = "0.15", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 36 };
                _cannyLowSlider.ValueChanged += (_, __) => lowVal.Text = _cannyLowSlider.Value.ToString("F2");
                p1.Children.Add(lowVal);
                MethodParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Верхний порог:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _cannyHighSlider = new Slider { Style = darkSlider, Minimum = 0.2, Maximum = 0.6, Value = 0.35, Width = 100 };
                p2.Children.Add(_cannyHighSlider);
                var highVal = new TextBlock { Text = "0.35", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 36 };
                _cannyHighSlider.ValueChanged += (_, __) => highVal.Text = _cannyHighSlider.Value.ToString("F2");
                p2.Children.Add(highVal);
                MethodParametersStack.Children.Add(p2);
            }
            else if (methodId == "DenoiseNLM")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal };
                p1.Children.Add(new TextBlock { Text = "σ (сглаживание):", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _nlmSigmaSlider = new Slider { Style = darkSlider, Minimum = 5, Maximum = 40, Value = 15, Width = 100 };
                p1.Children.Add(_nlmSigmaSlider);
                var nlmVal = new TextBlock { Text = "15", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 28 };
                _nlmSigmaSlider.ValueChanged += (_, __) => nlmVal.Text = ((int)_nlmSigmaSlider.Value).ToString();
                p1.Children.Add(nlmVal);
                MethodParametersStack.Children.Add(p1);
            }
            else if (methodId == "Deblur")
            {
                var p1 = new StackPanel { Orientation = Orientation.Horizontal };
                p1.Children.Add(new TextBlock { Text = "Итерации:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _deblurIterationsSlider = new Slider { Style = darkSlider, Minimum = 1, Maximum = 8, Value = 3, Width = 100, TickFrequency = 1, IsSnapToTickEnabled = true };
                p1.Children.Add(_deblurIterationsSlider);
                var debVal = new TextBlock { Text = "3", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 24 };
                _deblurIterationsSlider.ValueChanged += (_, __) => debVal.Text = ((int)_deblurIterationsSlider.Value).ToString();
                p1.Children.Add(debVal);
                MethodParametersStack.Children.Add(p1);
            }
            else if (methodId == "SeamCarving")
            {
                int maxCols = _originalBitmap != null ? Math.Max(1, _originalBitmap.Width - 10) : 100;
                int maxRows = _originalBitmap != null ? Math.Max(1, _originalBitmap.Height - 10) : 100;
                var p1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                p1.Children.Add(new TextBlock { Text = "Удалить столбцов:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _seamCarvingColsSlider = new Slider { Style = darkSlider, Minimum = 0, Maximum = maxCols, Value = Math.Min(20, maxCols), Width = 120, TickFrequency = 1, IsSnapToTickEnabled = true };
                p1.Children.Add(_seamCarvingColsSlider);
                var v1 = new TextBlock { Text = ((int)(_seamCarvingColsSlider.Value)).ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 28 };
                _seamCarvingColsSlider.ValueChanged += (_, __) => v1.Text = ((int)_seamCarvingColsSlider.Value).ToString();
                p1.Children.Add(v1);
                MethodParametersStack.Children.Add(p1);
                var p2 = new StackPanel { Orientation = Orientation.Horizontal };
                p2.Children.Add(new TextBlock { Text = "Удалить строк:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _seamCarvingRowsSlider = new Slider { Style = darkSlider, Minimum = 0, Maximum = maxRows, Value = Math.Min(20, maxRows), Width = 120, TickFrequency = 1, IsSnapToTickEnabled = true };
                p2.Children.Add(_seamCarvingRowsSlider);
                var v2 = new TextBlock { Text = ((int)(_seamCarvingRowsSlider.Value)).ToString(), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Foreground = textPrimary, MinWidth = 28 };
                _seamCarvingRowsSlider.ValueChanged += (_, __) => v2.Text = ((int)_seamCarvingRowsSlider.Value).ToString();
                p2.Children.Add(v2);
                MethodParametersStack.Children.Add(p2);
            }
            else if (methodId == "PseudoColor")
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                panel.Children.Add(new TextBlock { Text = "Цветовая схема:", Foreground = textPrimary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _pseudoColorCombo = new ComboBox { Style = darkComboBox, Width = 120 };
                _pseudoColorCombo.Items.Add("Jet (тепловая)");
                _pseudoColorCombo.Items.Add("Hot (огонь)");
                _pseudoColorCombo.Items.Add("Rainbow");
                _pseudoColorCombo.SelectedIndex = 0;
                panel.Children.Add(_pseudoColorCombo);
                MethodParametersStack.Children.Add(panel);
            }
            else if (Array.IndexOf(AvailableMethodIds, methodId) < 0)
            {
                MethodParametersStack.Children.Add(new TextBlock { Text = "Этот метод пока в разработке.", Foreground = textMuted, Margin = new Thickness(0, 8, 0, 0) });
            }

            MethodParametersPanel.Visibility = MethodParametersStack.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Считывает текущие значения параметров выбранного метода для PDF-отчёта.</summary>
        private Dictionary<string, string> CollectCurrentParameters()
        {
            var p = new Dictionary<string, string>();
            switch (_selectedMethodId)
            {
                case "WienerFilter":
                    if (_wienerRadiusSlider != null)
                        p["Радиус окна"] = ((int)_wienerRadiusSlider.Value).ToString();
                    if (_wienerNoiseTextBox != null)
                        p["Дисперсия шума"] = _wienerNoiseTextBox.Text;
                    break;
                case "Gamma":
                    if (_gammaSlider != null)
                        p["γ (гамма)"] = _gammaSlider.Value.ToString("F2");
                    break;
                case "UnsharpMask":
                    if (_unsharpStrengthSlider != null)
                        p["Сила k"] = _unsharpStrengthSlider.Value.ToString("F1");
                    if (_unsharpRadiusSlider != null)
                        p["Радиус размытия"] = ((int)_unsharpRadiusSlider.Value).ToString();
                    break;
                case "BilateralFilter":
                    if (_bilateralRadiusSlider != null)
                        p["Радиус"] = ((int)_bilateralRadiusSlider.Value).ToString();
                    if (_bilateralSigmaRSlider != null)
                        p["σ по яркости"] = ((int)_bilateralSigmaRSlider.Value).ToString();
                    break;
                case "Morphology":
                    if (_morphologyCombo != null && _morphologyCombo.SelectedItem != null)
                        p["Операция"] = _morphologyCombo.SelectedItem.ToString()!;
                    if (_morphologyRadiusSlider != null)
                        p["Радиус"] = ((int)_morphologyRadiusSlider.Value).ToString();
                    break;
                case "EdgeDetection":
                    if (_cannyLowSlider != null)
                        p["Нижний порог"] = _cannyLowSlider.Value.ToString("F2");
                    if (_cannyHighSlider != null)
                        p["Верхний порог"] = _cannyHighSlider.Value.ToString("F2");
                    break;
                case "DenoiseNLM":
                    if (_nlmSigmaSlider != null)
                        p["σ (сглаживание)"] = ((int)_nlmSigmaSlider.Value).ToString();
                    break;
                case "Deblur":
                    if (_deblurIterationsSlider != null)
                        p["Итерации"] = ((int)_deblurIterationsSlider.Value).ToString();
                    break;
                case "SeamCarving":
                    if (_seamCarvingColsSlider != null)
                        p["Удалить столбцов"] = ((int)_seamCarvingColsSlider.Value).ToString();
                    if (_seamCarvingRowsSlider != null)
                        p["Удалить строк"] = ((int)_seamCarvingRowsSlider.Value).ToString();
                    break;
                case "PseudoColor":
                    if (_pseudoColorCombo != null && _pseudoColorCombo.SelectedItem != null)
                        p["Цветовая схема"] = _pseudoColorCombo.SelectedItem.ToString()!;
                    break;
            }
            return p;
        }

        private void MethodApply_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null)
            {
                MessageBox.Show("Сначала загрузите изображение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_selectedMethodId) || Array.IndexOf(AvailableMethodIds, _selectedMethodId) < 0)
            {
                MessageBox.Show("Выберите метод слева.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Применить к оригиналу — сбросить прежнюю цепочку, сохранить состояние для undo
            PushUndo();
            _pipelineSteps.Clear();
            _pipelineFormulas.Clear();
            RunApply((Bitmap)_originalBitmap!.Clone(), isChainStep: false);
        }

        private void MethodChainApply_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null)
            {
                MessageBox.Show("Сначала загрузите изображение.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_processedBitmap == null)
            {
                MessageBox.Show("Сначала нажмите «Применить», чтобы получить первый результат.", "Цепочка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrEmpty(_selectedMethodId) || Array.IndexOf(AvailableMethodIds, _selectedMethodId) < 0)
            {
                MessageBox.Show("Выберите метод слева.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            // Сохраняем состояние для undo перед шагом цепочки
            PushUndo();
            RunApply((Bitmap)_processedBitmap!.Clone(), isChainStep: true);
        }

        /// <summary>
        /// Запускает обработку. <paramref name="sourceClone"/> — клон источника (освобождается внутри).
        /// <paramref name="isChainStep"/> = true → шаг цепочки, false → применение к оригиналу.
        /// </summary>
        private void RunApply(Bitmap sourceClone, bool isChainStep)
        {
            string methodId   = _selectedMethodId!;
            string methodName = _selectedMethodName ?? methodId;
            string methodIcon = _selectedMethodIcon ?? "◇";
            ApplyParams p     = CaptureApplyParams();

            LoadingOverlay.Visibility  = Visibility.Visible;
            MethodApplyButton.IsEnabled = false;
            MethodChainButton.IsEnabled = false;

            Task.Run(() => RunMethodInBackground(sourceClone, methodId, p))
                .ContinueWith(t =>
                {
                    sourceClone.Dispose();
                    Dispatcher.Invoke(() =>
                    {
                        LoadingOverlay.Visibility   = Visibility.Collapsed;
                        MethodApplyButton.IsEnabled = true;
                        try
                        {
                            if (t.IsFaulted && t.Exception != null)
                            {
                                MessageBox.Show($"Ошибка: {t.Exception.GetBaseException().Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            Bitmap? result = t.Result;
                            if (result != null)
                            {
                                SetProcessedImage(result);

                                // Метрики считаем к оригиналу, а не к предыдущему шагу
                                double ssim = double.NaN, psnr = double.NaN;
                                if (_originalBitmap != null &&
                                    result.Width  == _originalBitmap.Width &&
                                    result.Height == _originalBitmap.Height)
                                {
                                    try { ssim = Metrics.ComputeSSIM(_originalBitmap, result); } catch { }
                                    try { psnr = Metrics.ComputePSNR(_originalBitmap, result); } catch { }
                                }

                                string? thumbFile = SaveThumbnail(result, methodId);
                                result.Dispose();

                                AppSettings.Current.AddHistoryEntry(
                                    methodId, methodName, _currentImageFileName,
                                    ssim, psnr, thumbFile);

                                // Обновляем цепочку
                                _pipelineSteps.Add($"{methodIcon} {methodName}");
                                _pipelineFormulas.Add(_currentFormula ?? "—");
                                UpdatePipelineDisplay();
                            }
                            else if (IsAiMethod(methodId))
                            {
                                MessageBox.Show(
                                    "ONNX-модель Real-ESRGAN не найдена. Поместите файл Real-ESRGAN-General-x4v3.onnx в папку \"Models\" рядом с программой.",
                                    "Real-ESRGAN", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        finally { UpdateChainButton(); }
                    });
                }, TaskScheduler.Default);
        }

        // ── Undo ──────────────────────────────────────────────────────────

        private void PushUndo()
        {
            if (_undoStack.Count >= 5)
            {
                // убираем самое старое — Stack не поддерживает прямой доступ, конвертируем
                var list = new List<(Bitmap?, List<string>, List<string>)>(_undoStack);
                list[list.Count - 1].Item1?.Dispose();
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--) _undoStack.Push(list[i]);
            }
            _undoStack.Push((_processedBitmap != null ? (Bitmap)_processedBitmap.Clone() : null,
                             new List<string>(_pipelineSteps),
                             new List<string>(_pipelineFormulas)));
        }

        private void UndoLastStep()
        {
            if (_undoStack.Count == 0) return;
            var (bmp, steps, formulas) = _undoStack.Pop();
            _processedBitmap?.Dispose();
            _processedBitmap = bmp;
            _pipelineSteps.Clear();  _pipelineSteps.AddRange(steps);
            _pipelineFormulas.Clear(); _pipelineFormulas.AddRange(formulas);
            if (_processedBitmap != null)
            {
                ProcessedImageControl.Source = BitmapToImageSource(_processedBitmap);
                ProcessedPlaceholder.Visibility = Visibility.Collapsed;
            }
            else
            {
                ProcessedImageControl.Source = null;
                ProcessedPlaceholder.Visibility = Visibility.Visible;
                if (_viewCompare) { _viewCompare = false; ComparePanel.Visibility = Visibility.Collapsed; OriginalPanel.Visibility = Visibility.Visible; }
            }
            UpdateCompareTabVisibility();
            UpdateImageInfoPanel();
            UpdatePipelineDisplay();
            UpdateChainButton();
        }

        // ── Pipeline helpers ───────────────────────────────────────────────

        private void UpdateChainButton()
        {
            MethodChainButton.IsEnabled =
                _processedBitmap != null &&
                !string.IsNullOrEmpty(_selectedMethodId) &&
                Array.IndexOf(AvailableMethodIds, _selectedMethodId) >= 0;
        }

        private void PipelineReset_Click(object sender, RoutedEventArgs e)
        {
            _pipelineSteps.Clear();
            _pipelineFormulas.Clear();
            UpdatePipelineDisplay();
            MethodReset_Click(sender, e);
        }

        private void UpdatePipelineDisplay()
        {
            PipelineStepsPanel.Children.Clear();
            if (_pipelineSteps.Count == 0)
            {
                PipelineBar.Visibility = Visibility.Collapsed;
                return;
            }

            PipelineBar.Visibility = Visibility.Visible;

            AddPipelineNode("🖼 Оригинал", isSource: true);

            foreach (var step in _pipelineSteps)
            {
                // Стрелка
                PipelineStepsPanel.Children.Add(new TextBlock
                {
                    Text = " → ",
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush")
                });
                AddPipelineNode(step, isSource: false);
            }
        }

        private void AddPipelineNode(string label, bool isSource)
        {
            var bg = isSource
                ? (System.Windows.Media.Brush)FindResource("SurfaceInputBrush")
                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 99, 102, 241));
            var border = isSource
                ? (System.Windows.Media.Brush)FindResource("BorderSubtleBrush")
                : new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 99, 102, 241));
            var fg = isSource
                ? (System.Windows.Media.Brush)FindResource("TextMutedBrush")
                : (System.Windows.Media.Brush)FindResource("TextSecondary2Brush");

            var node = new Border
            {
                Background      = bg,
                BorderBrush     = border,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(5),
                Padding         = new Thickness(8, 3, 8, 3),
                Child           = new TextBlock
                {
                    Text                = label,
                    FontSize            = 11,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Foreground          = fg
                }
            };
            PipelineStepsPanel.Children.Add(node);
        }

        private ApplyParams CaptureApplyParams()
        {
            var p = new ApplyParams();
            if (_wienerRadiusSlider != null) p.WienerRadius = (int)_wienerRadiusSlider.Value;
            if (_wienerNoiseTextBox != null) p.WienerNoiseText = _wienerNoiseTextBox.Text ?? "10";
            if (_gammaSlider != null) p.Gamma = _gammaSlider.Value;
            if (_unsharpStrengthSlider != null) p.UnsharpStrength = _unsharpStrengthSlider.Value;
            if (_unsharpRadiusSlider != null) p.UnsharpRadius = (int)_unsharpRadiusSlider.Value;
            if (_bilateralRadiusSlider != null) p.BilateralRadius = (int)_bilateralRadiusSlider.Value;
            if (_bilateralSigmaRSlider != null) p.BilateralSigmaR = _bilateralSigmaRSlider.Value;
            if (_morphologyCombo != null) p.MorphologyOpenIndex = _morphologyCombo.SelectedIndex;
            if (_morphologyRadiusSlider != null) p.MorphologyRadius = (int)_morphologyRadiusSlider.Value;
            if (_cannyLowSlider != null) p.CannyLow = _cannyLowSlider.Value;
            if (_cannyHighSlider != null) p.CannyHigh = _cannyHighSlider.Value;
            if (_nlmSigmaSlider != null) p.NlmSigma = _nlmSigmaSlider.Value;
            if (_deblurIterationsSlider != null) p.DeblurIterations = (int)_deblurIterationsSlider.Value;
            if (_seamCarvingColsSlider != null) p.SeamCarvingCols = (int)_seamCarvingColsSlider.Value;
            if (_seamCarvingRowsSlider != null) p.SeamCarvingRows = (int)_seamCarvingRowsSlider.Value;
            if (_pseudoColorCombo != null) p.PseudoColorMap = Math.Max(0, _pseudoColorCombo.SelectedIndex);
            return p;
        }

        private static Bitmap? RunMethodInBackground(Bitmap source, string methodId, ApplyParams p)
        {
            return methodId switch
            {
                "LinearContrast" => ImageProcessors.LinearContrastStretch(source),
                "HistogramEq" => ImageProcessors.HistogramEqualization(source),
                "MedianFilter" => ImageProcessors.MedianFilter(source, radius: 1),
                "WienerFilter" => RunWiener(source, p),
                "Gamma" => ImageProcessors.GammaCorrection(source, gamma: p.Gamma, c: 1.0),
                "UnsharpMask" => ImageProcessors.UnsharpMask(source, strength: p.UnsharpStrength, blurRadius: p.UnsharpRadius),
                "BilateralFilter" => ImageProcessors.BilateralFilter(source, radius: p.BilateralRadius, sigmaSpace: 2, sigmaRange: p.BilateralSigmaR),
                "Morphology" => p.MorphologyOpenIndex == 0 ? ImageProcessors.MorphologyOpen(source, p.MorphologyRadius) : ImageProcessors.MorphologyClose(source, p.MorphologyRadius),
                "EdgeDetection" => ImageProcessors.CannyEdgeDetection(source, lowThreshold: p.CannyLow, highThreshold: p.CannyHigh),
                "DenoiseNLM" => ImageProcessors.DenoiseNLM(source, searchRadius: 3, patchRadius: 1, sigma: p.NlmSigma),
                "Deblur" => ImageProcessors.Deblur(source, iterations: p.DeblurIterations),
                "SeamCarving" => RunSeamCarving(source, p),
                "ESRGAN"      => AiImageProcessors.Esrgan(source),
                "CLAHE"       => ImageProcessors.CLAHE(source, tileSize: 64, clipLimit: 2.5),
                "MSRetinex"   => ImageProcessors.MultiScaleRetinex(source,
                                     sigmas:  new[] { 15.0, 80.0, 250.0 },
                                     weights: new[] { 1.0/3, 1.0/3, 1.0/3 }),
                "LogTransform" => ImageProcessors.LogarithmicTransform(source),
                "OtsuBinarize" => ImageProcessors.OtsuBinarization(source),
                "PseudoColor"  => ImageProcessors.PseudoColor(source, p.PseudoColorMap),
                _ => null
            };
        }

        private static Bitmap RunWiener(Bitmap source, ApplyParams p)
        {
            string text = (p.WienerNoiseText ?? "10").Replace(',', '.');
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double noiseVar))
                noiseVar = 10;
            return ImageProcessors.WienerFilter(source, windowRadius: p.WienerRadius, noiseVariance: noiseVar);
        }

        private static Bitmap RunSeamCarving(Bitmap source, ApplyParams p)
        {
            int w = source.Width, h = source.Height;
            int cols = Math.Clamp(p.SeamCarvingCols, 0, w - 1);
            int rows = Math.Clamp(p.SeamCarvingRows, 0, h - 1);
            return ImageProcessors.SeamCarving(source, removeColumns: cols, removeRows: rows);
        }

        private static bool IsAiMethod(string? id) => id == "ESRGAN";

        private void MethodReset_Click(object sender, RoutedEventArgs e)
        {
            _processedBitmap?.Dispose();
            _processedBitmap = null;
            ProcessedImageControl.Source = null;
            ProcessedPlaceholder.Visibility = Visibility.Visible;
            if (_viewCompare)
            {
                _viewCompare = false;
                ComparePanel.Visibility = Visibility.Collapsed;
                OriginalPanel.Visibility = Visibility.Visible;
            }
            UpdateCompareTabVisibility();
            UpdateImageInfoPanel();
            _pipelineSteps.Clear();
            _pipelineFormulas.Clear();
            _undoStack.Clear();
            UpdatePipelineDisplay();
            UpdateChainButton();
            Title = "ImageUP — улучшение качества изображений";
        }

        public void SetOriginalImage(Bitmap bmp)
        {
            _originalBitmap?.Dispose();
            _originalBitmap = (Bitmap)bmp.Clone();

            OriginalImageControl.Source = BitmapToImageSource(_originalBitmap);
            LoadZoneBorder.Visibility = Visibility.Collapsed;

            _originalZoomScale = 1.0;
            OriginalImageScale.ScaleX = OriginalImageScale.ScaleY = 1.0;
            OriginalScrollViewer.ScrollToHorizontalOffset(0);
            OriginalScrollViewer.ScrollToVerticalOffset(0);
            UpdateZoomLabels();

            UpdateImageInfoPanel();
            UpdateCompareTabVisibility();
            double ssim = MetricsComputeSafe();
            Title = double.IsNaN(ssim) ? "ImageUP" : $"ImageUP — SSIM = {ssim:F4}";
        }

        public void SetProcessedImage(Bitmap bmp)
        {
            _processedBitmap?.Dispose();
            _processedBitmap = (Bitmap)bmp.Clone();

            ProcessedPlaceholder.Visibility = Visibility.Collapsed;
            ProcessedImageControl.Source = BitmapToImageSource(_processedBitmap);

            _processedZoomScale = 1.0;
            ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = 0.98;
            ProcessedImageControl.Opacity = 0;
            ProcessedScrollViewer.ScrollToHorizontalOffset(0);
            ProcessedScrollViewer.ScrollToVerticalOffset(0);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleOut = new DoubleAnimation(0.98, 1.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            scaleOut.Completed += (_, _) =>
            {
                ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = 1.0;
                ProcessedImageControl.Opacity = 1;
                _processedZoomScale = 1.0;
                ProcessedImageControl.BeginAnimation(UIElement.OpacityProperty, null);
                ProcessedImageScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                ProcessedImageScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
                UpdateZoomLabels();
            };
            ProcessedImageControl.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ProcessedImageScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleOut);
            ProcessedImageScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleOut);

            UpdateImageInfoPanel();
            UpdateCompareTabVisibility();
            double ssim = MetricsComputeSafe();
            Title = double.IsNaN(ssim) ? "ImageUP" : $"ImageUP — SSIM = {ssim:F4}";
        }

        private void History_Click(object sender, RoutedEventArgs e) => OpenHistoryWindow();

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow { Owner = this };
            settings.ShowDialog();
        }

        private void QualityCard_OpenClick(object sender, RoutedEventArgs e)
        {
            var qualityWindow = new QualityWindow(_originalBitmap, _processedBitmap);
            qualityWindow.Owner = this;
            qualityWindow.Show();
        }

        private void MethodCard_MouseEnter(object sender, MouseEventArgs e)
        {
            _methodPanelHideTimer?.Stop();
            if (sender is not FrameworkElement fe || fe.DataContext is not MethodItem item)
                return;
            MethodDetailTitle.Text = item.Name;
            BuildFormulaPanel(MethodDetailFormulaPanel, string.IsNullOrEmpty(item.Formula) ? "—" : item.Formula);
            MethodDetailPrinciple.Text = string.IsNullOrEmpty(item.Principle) ? "—" : item.Principle;
        }

        private static void BuildFormulaPanel(StackPanel panel, string formula)
        {
            panel.Children.Clear();
            var fontFamily = new System.Windows.Media.FontFamily("Cambria Math, Segoe UI, serif");
            const int fontSize = 15;
            var tb = CreateFormulaTextBlock(formula.Replace("\n", " ").Trim(), fontSize, fontFamily);
            // SetResourceReference обеспечивает автообновление цвета при смене темы
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary2Brush");
            tb.TextWrapping = TextWrapping.Wrap;
            tb.LineHeight = 26;
            panel.Children.Add(tb);
        }

        /// <summary>
        /// Строит TextBlock с поддержкой подстрочных (_x или _{текст}) и
        /// надстрочных (^x или ^{текст}) символов.
        /// Цвет не устанавливается — наследуется от родительского TextBlock.
        /// </summary>
        private static TextBlock CreateFormulaTextBlock(string text, int fontSize,
            System.Windows.Media.FontFamily fontFamily)
        {
            var tb = new TextBlock
            {
                FontSize   = fontSize,
                FontFamily = fontFamily,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment       = TextAlignment.Left,
                TextWrapping        = TextWrapping.Wrap
            };
            int i = 0;
            while (i < text.Length)
            {
                if ((text[i] == '_' || text[i] == '^') && i + 1 < text.Length)
                {
                    bool isSub = text[i] == '_';
                    i++;
                    string piece;
                    if (i < text.Length && text[i] == '{')
                    {
                        i++; // skip '{'
                        int s = i;
                        while (i < text.Length && text[i] != '}') i++;
                        piece = text.Substring(s, i - s);
                        if (i < text.Length) i++; // skip '}'
                    }
                    else
                    {
                        piece = text[i].ToString();
                        i++;
                    }
                    tb.Inlines.Add(new Run(piece)
                    {
                        BaselineAlignment = isSub ? BaselineAlignment.Subscript : BaselineAlignment.Superscript,
                        FontSize   = fontSize * 0.70,
                        FontFamily = fontFamily
                        // Foreground не задаём — наследуется от TextBlock
                    });
                    continue;
                }
                int start = i;
                while (i < text.Length && text[i] != '_' && text[i] != '^') i++;
                if (start < i)
                    tb.Inlines.Add(new Run(text.Substring(start, i - start))
                        { FontFamily = fontFamily, FontSize = fontSize });
            }
            return tb;
        }

        private void MethodCard_MouseLeave(object sender, MouseEventArgs e)
        {
            _methodPanelHideTimer?.Stop();
            _methodPanelHideTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _methodPanelHideTimer.Tick += (_, __) =>
            {
                _methodPanelHideTimer?.Stop();
            };
            _methodPanelHideTimer.Start();
        }

        private void MethodDetailOverlay_MouseEnter(object sender, MouseEventArgs e)
        {
            _methodPanelHideTimer?.Stop();
        }

        private void MethodDetailOverlay_MouseLeave(object sender, MouseEventArgs e)
        {
        }

        private void QualityCard_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not Border card) return;
            var curtain = card.FindName("QualityCurtain") as Border;
            if (curtain != null)
                AnimateOpacity(curtain, 1, TimeSpan.FromMilliseconds(250));
        }

        private void QualityCard_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is not Border card) return;
            var curtain = card.FindName("QualityCurtain") as Border;
            if (curtain != null)
                AnimateOpacity(curtain, 0, TimeSpan.FromMilliseconds(200));
        }

        private static void AnimateOpacity(Border element, double to, TimeSpan duration)
        {
            var anim = new DoubleAnimation(to, duration);
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        #region Загрузка / сохранение

        private void LoadZone_Click(object sender, RoutedEventArgs e) => LoadImageFromFile();

        private void LoadZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
        }

        private void LoadZone_DragLeave(object sender, DragEventArgs e) { }

        private void LoadZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    LoadImageFromFile(files[0]);
            }
        }

        private void LoadImage_Click(object sender, RoutedEventArgs e) => LoadImageFromFile();

        /// <summary>Публичный вход для WelcomeWindow: открыть конкретный файл.</summary>
        public void LoadImageFromPath(string path) => LoadImageFromFile(path);

        private void LoadImageFromFile(string? path = null)
        {
            if (path == null)
            {
                var ofd = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
                if (ofd.ShowDialog() != true) return;
                path = ofd.FileName;
            }
            try
            {
                _originalBitmap?.Dispose();
                _processedBitmap?.Dispose();
                _originalBitmap = new Bitmap(path);
                _currentImageFileName = System.IO.Path.GetFileName(path);
                _currentImageFilePath = path;
                _processedBitmap = null;
                OriginalImageControl.Source = BitmapToImageSource(_originalBitmap);
                ProcessedImageControl.Source = null;
                LoadZoneBorder.Visibility = Visibility.Collapsed;
                ProcessedPlaceholder.Visibility = Visibility.Visible;
                if (_viewCompare) { _viewCompare = false; ComparePanel.Visibility = Visibility.Collapsed; OriginalPanel.Visibility = Visibility.Visible; }
                ResetZoom();
                UpdateCompareTabVisibility();
                UpdateImageInfoPanel();
                _pipelineSteps.Clear();
                _pipelineFormulas.Clear();
                _undoStack.Clear();
                UpdatePipelineDisplay();
                UpdateChainButton();
                AppSettings.Current.AddRecentFile(path);
                Title = "ImageUP — изображение загружено";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            if (_processedBitmap == null)
            {
                MessageBox.Show("Нет обработанного изображения для сохранения.",
                    "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp",
                FileName = "output.png"
            };

            if (sfd.ShowDialog() == true)
            {
                string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                var format = System.Drawing.Imaging.ImageFormat.Png;
                if (ext == ".jpg" || ext == ".jpeg")
                    format = System.Drawing.Imaging.ImageFormat.Jpeg;
                else if (ext == ".bmp")
                    format = System.Drawing.Imaging.ImageFormat.Bmp;

                _processedBitmap.Save(sfd.FileName, format);
            }
        }

        #endregion

        #region Вспомогательные методы

        internal static BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        private void ResetZoom()
        {
            _originalZoomScale = _processedZoomScale = _compareZoomScale = 1.0;
            OriginalImageScale.ScaleX = OriginalImageScale.ScaleY = 1.0;
            ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = 1.0;
            CompareImageScale.ScaleX = CompareImageScale.ScaleY = 1.0;
            OriginalScrollViewer.ScrollToHorizontalOffset(0);
            OriginalScrollViewer.ScrollToVerticalOffset(0);
            ProcessedScrollViewer.ScrollToHorizontalOffset(0);
            ProcessedScrollViewer.ScrollToVerticalOffset(0);
            CompareScrollViewer.ScrollToHorizontalOffset(0);
            CompareScrollViewer.ScrollToVerticalOffset(0);
            UpdateZoomLabels();
        }

        private void UpdateZoomLabels()
        {
            UpdateCenterZoomText();
        }

        private void UpdateImageInfoPanel()
        {
            if (_originalBitmap == null)
            {
                InfoOrigDimText.Text = "—";
                InfoFileMeta.Text = "";
                InfoSepArrow.Visibility = Visibility.Collapsed;
                InfoProcDimText.Visibility = Visibility.Collapsed;
                InfoPanelDimText.Text = "—";
                InfoPanelModeText.Text = "—";
                InfoPanelFileText.Text = "—";
                InfoPanelProcRow.Visibility = Visibility.Collapsed;
                HistLegendProcDot.Visibility = Visibility.Collapsed;
                HistLegendProcText.Visibility = Visibility.Collapsed;
                DrawHistogram(_originalBitmap, _processedBitmap);
                return;
            }

            var orig = _originalBitmap;
            // Статус-бар
            InfoOrigDimText.Text = $"{orig.Width} × {orig.Height} px";

            // Формат файла
            string fmt = "—";
            if (_currentImageFilePath != null)
                fmt = Path.GetExtension(_currentImageFilePath).TrimStart('.').ToUpperInvariant();
            else if (_currentImageFileName != null)
                fmt = Path.GetExtension(_currentImageFileName).TrimStart('.').ToUpperInvariant();

            // Размер файла
            string sizeStr = "";
            if (_currentImageFilePath != null && File.Exists(_currentImageFilePath))
            {
                long bytes = new FileInfo(_currentImageFilePath).Length;
                sizeStr = bytes >= 1_048_576
                    ? $" · {bytes / 1_048_576.0:F1} МБ"
                    : $" · {bytes / 1024.0:F0} КБ";
            }

            string fileName = _currentImageFileName ?? "clipboard.png";
            InfoFileMeta.Text = $"· {fileName}{sizeStr}";

            // Правая панель — детальная информация
            InfoPanelDimText.Text = $"{orig.Width} × {orig.Height} px";

            // Каналы (по формату пикселей)
            string channels = orig.PixelFormat.ToString().Contains("Argb") ? "RGBA" : "RGB";
            float dpiX = orig.HorizontalResolution;
            InfoPanelModeText.Text = $"{channels} · {dpiX:F0} DPI";

            string fileSizeFull = "";
            if (_currentImageFilePath != null && File.Exists(_currentImageFilePath))
            {
                long b = new FileInfo(_currentImageFilePath).Length;
                fileSizeFull = b >= 1_048_576 ? $"{b / 1_048_576.0:F1} МБ · " : $"{b / 1024.0:F0} КБ · ";
            }
            InfoPanelFileText.Text = $"{fileSizeFull}{fmt}";

            // Обработанное изображение
            if (_processedBitmap != null)
            {
                InfoSepArrow.Visibility = Visibility.Visible;
                InfoProcDimText.Visibility = Visibility.Visible;
                InfoProcDimText.Text = $"{_processedBitmap.Width} × {_processedBitmap.Height} px";

                InfoPanelProcRow.Visibility = Visibility.Visible;
                bool sameSize = _processedBitmap.Width == orig.Width && _processedBitmap.Height == orig.Height;
                InfoPanelProcText.Text = sameSize
                    ? $"{_processedBitmap.Width} × {_processedBitmap.Height} px"
                    : $"{_processedBitmap.Width} × {_processedBitmap.Height} px  (×{(double)_processedBitmap.Width / orig.Width:F0} масштаб)";

                HistLegendProcDot.Visibility = Visibility.Visible;
                HistLegendProcText.Visibility = Visibility.Visible;
            }
            else
            {
                InfoSepArrow.Visibility = Visibility.Collapsed;
                InfoProcDimText.Visibility = Visibility.Collapsed;
                InfoPanelProcRow.Visibility = Visibility.Collapsed;
                HistLegendProcDot.Visibility = Visibility.Collapsed;
                HistLegendProcText.Visibility = Visibility.Collapsed;
            }

            DrawHistogram(_originalBitmap, _processedBitmap);
        }

        // ── Гистограмма ────────────────────────────────────────────────────

        private void HistogramCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawHistogram(_originalBitmap, _processedBitmap);
        }

        private void DrawHistogram(Bitmap? orig, Bitmap? proc)
        {
            HistogramCanvas.Children.Clear();
            if (orig == null) return;

            double canvasW = HistogramCanvas.ActualWidth;
            double canvasH = HistogramCanvas.ActualHeight;
            if (canvasW < 10 || canvasH < 10) return;

            int[] histOrig = ComputeLuminanceHistogram(orig);
            int[]? histProc = proc != null ? ComputeLuminanceHistogram(proc) : null;

            double maxVal = (double)histOrig.Max();
            if (histProc != null) maxVal = Math.Max(maxVal, histProc.Max());
            if (maxVal == 0) return;

            double barW = canvasW / 256.0;

            // Оригинал — accent синий
            for (int i = 0; i < 256; i++)
            {
                double h = histOrig[i] / maxVal * canvasH;
                if (h < 0.5) continue;
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width  = Math.Max(1, barW),
                    Height = h,
                    Fill   = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 99, 102, 241))
                };
                Canvas.SetLeft(rect, i * barW);
                Canvas.SetBottom(rect, 0);
                HistogramCanvas.Children.Add(rect);
            }

            // Обработанное — зелёный поверх
            if (histProc != null)
            {
                for (int i = 0; i < 256; i++)
                {
                    double h = histProc[i] / maxVal * canvasH;
                    if (h < 0.5) continue;
                    var rect = new System.Windows.Shapes.Rectangle
                    {
                        Width  = Math.Max(1, barW),
                        Height = h,
                        Fill   = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 34, 197, 94))
                    };
                    Canvas.SetLeft(rect, i * barW);
                    Canvas.SetBottom(rect, 0);
                    HistogramCanvas.Children.Add(rect);
                }
            }
        }

        private static int[] ComputeLuminanceHistogram(Bitmap bmp)
        {
            var hist = new int[256];
            // Считаем по уменьшенной копии (макс. 300×300) — быстро даже для 4K
            int maxSide = 300;
            double ratio = Math.Min((double)maxSide / bmp.Width, (double)maxSide / bmp.Height);
            ratio = Math.Min(ratio, 1.0);
            int sw = Math.Max(1, (int)(bmp.Width * ratio));
            int sh = Math.Max(1, (int)(bmp.Height * ratio));
            using var small = new Bitmap(bmp, sw, sh);
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    var c = small.GetPixel(x, y);
                    int lum = (c.R * 299 + c.G * 587 + c.B * 114) / 1000;
                    hist[lum]++;
                }
            return hist;
        }

        private static readonly string ThumbnailDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "ImageUP", "thumbnails");

        private static string? SaveThumbnail(Bitmap bmp, string methodId)
        {
            try
            {
                Directory.CreateDirectory(ThumbnailDir);
                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{methodId}.jpg";
                var fullPath = Path.Combine(ThumbnailDir, fileName);
                using var thumb = CreateThumbnail(bmp, 200, 140);
                thumb.Save(fullPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                return fileName;
            }
            catch { return null; }
        }

        private static Bitmap CreateThumbnail(Bitmap src, int maxW, int maxH)
        {
            double ratio = Math.Min((double)maxW / src.Width, (double)maxH / src.Height);
            int w = Math.Max(1, (int)(src.Width  * ratio));
            int h = Math.Max(1, (int)(src.Height * ratio));
            var thumb = new Bitmap(w, h);
            using var g = System.Drawing.Graphics.FromImage(thumb);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, w, h);
            return thumb;
        }

        private double MetricsComputeSafe()
        {
            if (_originalBitmap == null || _processedBitmap == null)
                return double.NaN;
            if (_originalBitmap.Width != _processedBitmap.Width || _originalBitmap.Height != _processedBitmap.Height)
                return double.NaN; // SSIM/PSNR требуют один размер (напр. после Real-ESRGAN размеры различаются)
            return Metrics.ComputeSSIM(_originalBitmap, _processedBitmap);
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Экспорт сравнения

        private void ExportComparison_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null || _processedBitmap == null)
            {
                MessageBox.Show("Для экспорта сравнения нужно загрузить изображение и применить метод.",
                    "Сравнение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter   = "PNG изображение|*.png|JPEG изображение|*.jpg",
                FileName = $"ImageUP_Compare_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                int divider = 4;
                int w = _originalBitmap.Width + _processedBitmap.Width + divider;
                int h = Math.Max(_originalBitmap.Height, _processedBitmap.Height);

                using var canvas = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(canvas);

                g.Clear(System.Drawing.Color.FromArgb(30, 30, 40)); // тёмный фон-разделитель

                g.DrawImage(_originalBitmap, 0, (h - _originalBitmap.Height) / 2,
                            _originalBitmap.Width, _originalBitmap.Height);
                g.DrawImage(_processedBitmap, _originalBitmap.Width + divider, (h - _processedBitmap.Height) / 2,
                            _processedBitmap.Width, _processedBitmap.Height);

                // Подписи
                using var font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 255, 255, 255));
                using var shadow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(120, 0, 0, 0));
                g.DrawString("Оригинал", font, shadow, 12, 12);
                g.DrawString("Оригинал", font, brush, 10, 10);
                string label = _selectedMethodName ?? "Результат";
                g.DrawString(label, font, shadow, _originalBitmap.Width + divider + 12, 12);
                g.DrawString(label, font, brush,  _originalBitmap.Width + divider + 10, 10);

                bool isJpeg = sfd.FilterIndex == 2;
                if (isJpeg)
                    canvas.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
                else
                    canvas.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении сравнения:\n{ex.Message}",
                    "Сравнение", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        // ──────────────────────────────────────────────────────────────────
        #region Экспорт PDF-отчёта

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_originalBitmap == null)
            {
                MessageBox.Show("Сначала загрузите изображение.", "PDF-отчёт",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter   = "PDF-документ|*.pdf",
                FileName = $"ImageUP_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };
            if (sfd.ShowDialog() != true) return;

            // Метрики
            double ssim = double.NaN, psnr = double.NaN;
            double sharpOrig = double.NaN, sharpProc = double.NaN;
            if (_processedBitmap != null)
            {
                if (_originalBitmap.Width  == _processedBitmap.Width &&
                    _originalBitmap.Height == _processedBitmap.Height)
                {
                    try { ssim = Metrics.ComputeSSIM(_originalBitmap, _processedBitmap); } catch { }
                    try { psnr = Metrics.ComputePSNR(_originalBitmap, _processedBitmap); } catch { }
                }
                try { sharpOrig = Metrics.ComputeSharpness(_originalBitmap); } catch { }
                try { sharpProc = Metrics.ComputeSharpness(_processedBitmap); } catch { }
            }

            string origDims = $"{_originalBitmap.Width} × {_originalBitmap.Height} px";
            string procDims = _processedBitmap != null
                ? $"{_processedBitmap.Width} × {_processedBitmap.Height} px" : "—";

            // Параметры обработки
            var parameters = CollectCurrentParameters();

            // Принцип работы метода
            string? principle = _selectedMethodItem?.Principle;

            // Метаданные изображения
            ImageMetadata? metadata = null;
            if (_originalBitmap != null)
            {
                string dpiStr  = $"{(int)Math.Round(_originalBitmap.HorizontalResolution)} DPI";
                string fmt     = _originalBitmap.PixelFormat.ToString();
                string colorMode = fmt.Contains("Alpha") || fmt.Contains("Argb") ? "RGBA"
                                 : fmt.Contains("Gray") ? "Grayscale" : "RGB";
                string fileFormat = !string.IsNullOrEmpty(_currentImageFilePath)
                    ? System.IO.Path.GetExtension(_currentImageFilePath).TrimStart('.').ToUpperInvariant()
                    : "—";
                string fileSizeStr = "—";
                if (!string.IsNullOrEmpty(_currentImageFilePath) && System.IO.File.Exists(_currentImageFilePath))
                {
                    long kb = new System.IO.FileInfo(_currentImageFilePath).Length / 1024;
                    fileSizeStr = $"{kb} КБ";
                }
                metadata = new ImageMetadata(dpiStr, colorMode, fileFormat, fileSizeStr);
            }

            // Пайплайн с формулами
            var pipelineWithFormulas = new List<(string name, string formula)>();
            for (int i = 0; i < _pipelineSteps.Count; i++)
            {
                string formula = i < _pipelineFormulas.Count ? _pipelineFormulas[i] : "—";
                pipelineWithFormulas.Add((_pipelineSteps[i], formula));
            }

            try
            {
                PdfReportGenerator.Generate(
                    outputPath:  sfd.FileName,
                    original:    _originalBitmap!,
                    processed:   _processedBitmap,
                    methodName:  _selectedMethodName ?? "—",
                    formula:     _currentFormula    ?? "—",
                    principle:   principle,
                    fileName:    _currentImageFileName ?? "—",
                    origDims:    origDims,
                    procDims:    procDims,
                    ssim:        ssim,
                    psnr:        psnr,
                    sharpOrig:   sharpOrig,
                    sharpProc:   sharpProc,
                    pipeline:    pipelineWithFormulas,
                    parameters:  parameters.Count > 0 ? parameters : null,
                    metadata:    metadata);

                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании PDF:\n{ex.Message}",
                    "PDF-отчёт", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenerateReportHtml_UNUSED(
            string origB64, string procB64,
            double ssim, double psnr,
            double sharpOrig, double sharpProc)
        {
            string method = _selectedMethodName ?? "—";
            string formula = _currentFormula ?? "—";
            string fileName = _currentImageFileName ?? "—";
            string date = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            string origDims = _originalBitmap != null
                ? $"{_originalBitmap.Width} × {_originalBitmap.Height} px" : "—";
            string procDims = _processedBitmap != null
                ? $"{_processedBitmap.Width} × {_processedBitmap.Height} px" : "—";

            // Форматирование метрик
            string fmtSsim  = double.IsNaN(ssim)  ? "— (разный размер)" : $"{ssim:F4}";
            string fmtPsnr  = double.IsNaN(psnr)  ? "— (разный размер)" : $"{psnr:F2} дБ";
            string fmtShO   = double.IsNaN(sharpOrig) ? "—" : $"{sharpOrig:F0}";
            string fmtShP   = double.IsNaN(sharpProc) ? "—" : $"{sharpProc:F0}";
            string ssimColor = !double.IsNaN(ssim) && ssim > 0.85 ? "#22c55e" : "#f59e0b";
            string psnrColor = !double.IsNaN(psnr) && psnr > 30   ? "#22c55e" : "#f59e0b";

            string procSection = procB64.Length > 0 ? $@"
      <div class=""img-box"">
        <img src=""data:image/png;base64,{procB64}"" alt=""Результат""/>
        <div class=""img-label"">После обработки · {procDims}</div>
      </div>" : "";

            string metricsRows = $@"
      <div class=""metric-row"">
        <span class=""metric-label"">SSIM (структурное сходство)</span>
        <span class=""metric-value"" style=""color:{ssimColor}"">{fmtSsim}</span>
      </div>
      <div class=""metric-row"">
        <span class=""metric-label"">PSNR (пиковое отношение сигнал/шум)</span>
        <span class=""metric-value"" style=""color:{psnrColor}"">{fmtPsnr}</span>
      </div>
      <div class=""metric-row"">
        <span class=""metric-label"">Резкость — оригинал (дисп. лапласиана)</span>
        <span class=""metric-value"">{fmtShO}</span>
      </div>
      <div class=""metric-row"" style=""border:none"">
        <span class=""metric-label"">Резкость — результат</span>
        <span class=""metric-value"">{fmtShP}</span>
      </div>";

            return $@"<!DOCTYPE html>
<html lang=""ru"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>ImageUP — Отчёт об обработке</title>
<style>
  *{{box-sizing:border-box;margin:0;padding:0}}
  body{{font-family:'Segoe UI',Arial,sans-serif;background:#0c0c12;color:#e4e4e7;padding:32px 16px}}
  .container{{max-width:960px;margin:0 auto}}
  header{{margin-bottom:32px}}
  header h1{{font-size:26px;font-weight:700;background:linear-gradient(90deg,#6366f1,#8b5cf6);
    -webkit-background-clip:text;-webkit-text-fill-color:transparent;margin-bottom:6px}}
  .meta{{font-size:13px;color:#52525b}}
  .card{{background:#16161e;border:1px solid #27272a;border-radius:14px;padding:22px;margin-bottom:18px}}
  .section-title{{font-size:11px;font-weight:700;color:#52525b;text-transform:uppercase;
    letter-spacing:.1em;margin-bottom:14px}}
  .images{{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:16px}}
  .img-box img{{width:100%;border-radius:10px;border:1px solid #27272a;display:block}}
  .img-label{{font-size:12px;color:#71717a;margin-top:8px;text-align:center}}
  h2.method-name{{font-size:20px;margin-bottom:10px}}
  .formula{{font-family:'Cambria Math','Times New Roman',serif;background:#1a1a26;
    border:1px solid #27272a;border-radius:8px;padding:14px 16px;font-size:15px;
    color:#c4b5fd;line-height:1.7;margin-top:2px}}
  .metric-row{{display:flex;justify-content:space-between;align-items:center;
    padding:9px 0;border-bottom:1px solid #27272a}}
  .metric-label{{font-size:13px;color:#a1a1aa}}
  .metric-value{{font-size:13px;font-weight:700}}
  footer{{margin-top:32px;text-align:center;font-size:11px;color:#3f3f46}}
</style>
</head>
<body>
<div class=""container"">
  <header>
    <h1>ImageUP — Отчёт об обработке</h1>
    <div class=""meta"">Файл: {fileName} · Дата: {date}</div>
  </header>

  <div class=""card"">
    <div class=""section-title"">Изображения</div>
    <div class=""images"">
      <div class=""img-box"">
        <img src=""data:image/png;base64,{origB64}"" alt=""Оригинал""/>
        <div class=""img-label"">Оригинал · {origDims}</div>
      </div>{procSection}
    </div>
  </div>

  <div class=""card"">
    <div class=""section-title"">Метод обработки</div>
    <h2 class=""method-name"">{HtmlEncode(method)}</h2>
    <div class=""formula"">{HtmlEncode(formula)}</div>
  </div>

  <div class=""card"">
    <div class=""section-title"">Метрики качества</div>
    {metricsRows}
  </div>

  <footer>Создано с помощью ImageUP · {date}</footer>
</div>
</body>
</html>";
        }

        private static string HtmlEncode(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        private static string BitmapToBase64(Bitmap bmp)
        {
            // Уменьшаем до макс. 800px по длинной стороне для компактности HTML
            int maxSide = 800;
            double ratio = Math.Min((double)maxSide / bmp.Width, (double)maxSide / bmp.Height);
            ratio = Math.Min(ratio, 1.0);
            int w = Math.Max(1, (int)(bmp.Width * ratio));
            int h = Math.Max(1, (int)(bmp.Height * ratio));
            using var thumb = new Bitmap(bmp, w, h);
            using var ms = new MemoryStream();
            var jpegCodec = System.Drawing.Imaging.ImageCodecInfo
                .GetImageEncoders()
                .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
            var encParams = new System.Drawing.Imaging.EncoderParameters(1);
            encParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, 85L);
            thumb.Save(ms, jpegCodec, encParams);
            return Convert.ToBase64String(ms.ToArray());
        }

        #endregion

        #region Зум колёсиком и панорама перетаскиванием

        private void ApplyZoomOriginal(double delta, System.Windows.Point mousePos)
        {
            if (OriginalImageControl.Source == null) return;
            double oldScale = _originalZoomScale;
            _originalZoomScale *= (delta > 0 ? ZoomStep : 1.0 / ZoomStep);
            _originalZoomScale = Math.Clamp(_originalZoomScale, ZoomMin, ZoomMax);
            OriginalImageScale.ScaleX = OriginalImageScale.ScaleY = _originalZoomScale;
            UpdateZoomLabels();
            UpdateCenterZoomText();
        }

        private void ApplyZoomProcessed(double delta, System.Windows.Point mousePos)
        {
            if (ProcessedImageControl.Source == null) return;
            double oldScale = _processedZoomScale;
            _processedZoomScale *= (delta > 0 ? ZoomStep : 1.0 / ZoomStep);
            _processedZoomScale = Math.Clamp(_processedZoomScale, ZoomMin, ZoomMax);
            ProcessedImageScale.ScaleX = ProcessedImageScale.ScaleY = _processedZoomScale;
            UpdateZoomLabels();
            UpdateCenterZoomText();
        }

        private void ApplyZoomCompare(double delta, System.Windows.Point mousePos)
        {
            if (CompareOriginalImage.Source == null) return;
            _compareZoomScale *= (delta > 0 ? ZoomStep : 1.0 / ZoomStep);
            _compareZoomScale = Math.Clamp(_compareZoomScale, ZoomMin, ZoomMax);
            CompareImageScale.ScaleX = CompareImageScale.ScaleY = _compareZoomScale;
            UpdateZoomLabels();
            UpdateCenterZoomText();
        }

        private void OriginalScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (OriginalImageControl.Source == null) return;
            e.Handled = true;
            ApplyZoomOriginal(e.Delta, e.GetPosition(OriginalScrollViewer));
        }

        private void ProcessedScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ProcessedImageControl.Source == null) return;
            e.Handled = true;
            ApplyZoomProcessed(e.Delta, e.GetPosition(ProcessedScrollViewer));
        }

        private void CompareScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (CompareOriginalImage.Source == null) return;
            e.Handled = true;
            ApplyZoomCompare(e.Delta, e.GetPosition(CompareScrollViewer));
        }

        private void OriginalImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (OriginalImageControl.Source == null) return;
            _originalPanning = true;
            _originalPanStart = e.GetPosition(OriginalScrollViewer);
            _originalScrollStartX = OriginalScrollViewer.HorizontalOffset;
            _originalScrollStartY = OriginalScrollViewer.VerticalOffset;
            OriginalImageContainer.CaptureMouse();
            OriginalImageContainer.Cursor = Cursors.Hand;
        }

        private void OriginalImage_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_originalPanning) return;
            var pos = e.GetPosition(OriginalScrollViewer);
            double dx = _originalPanStart.X - pos.X;
            double dy = _originalPanStart.Y - pos.Y;
            OriginalScrollViewer.ScrollToHorizontalOffset(_originalScrollStartX + dx);
            OriginalScrollViewer.ScrollToVerticalOffset(_originalScrollStartY + dy);
        }

        private void OriginalImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _originalPanning = false;
            OriginalImageContainer.ReleaseMouseCapture();
            OriginalImageContainer.Cursor = Cursors.Arrow;
        }

        private void OriginalImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_originalPanning)
            {
                _originalPanning = false;
                OriginalImageContainer.ReleaseMouseCapture();
                OriginalImageContainer.Cursor = Cursors.Arrow;
            }
        }

        private void ProcessedImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ProcessedImageControl.Source == null) return;
            _processedPanning = true;
            _processedPanStart = e.GetPosition(ProcessedScrollViewer);
            _processedScrollStartX = ProcessedScrollViewer.HorizontalOffset;
            _processedScrollStartY = ProcessedScrollViewer.VerticalOffset;
            ProcessedImageContainer.CaptureMouse();
            ProcessedImageContainer.Cursor = Cursors.Hand;
        }

        private void ProcessedImage_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_processedPanning) return;
            var pos = e.GetPosition(ProcessedScrollViewer);
            double dx = _processedPanStart.X - pos.X;
            double dy = _processedPanStart.Y - pos.Y;
            ProcessedScrollViewer.ScrollToHorizontalOffset(_processedScrollStartX + dx);
            ProcessedScrollViewer.ScrollToVerticalOffset(_processedScrollStartY + dy);
        }

        private void ProcessedImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _processedPanning = false;
            ProcessedImageContainer.ReleaseMouseCapture();
            ProcessedImageContainer.Cursor = Cursors.Arrow;
        }

        private void ProcessedImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_processedPanning)
            {
                _processedPanning = false;
                ProcessedImageContainer.ReleaseMouseCapture();
                ProcessedImageContainer.Cursor = Cursors.Arrow;
            }
        }

        private void CompareImage_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (CompareOriginalImage.Source == null) return;
            _comparePanning = true;
            _comparePanStart = e.GetPosition(CompareScrollViewer);
            _compareScrollStartX = CompareScrollViewer.HorizontalOffset;
            _compareScrollStartY = CompareScrollViewer.VerticalOffset;
            CompareZoomContainer.CaptureMouse();
            CompareZoomContainer.Cursor = Cursors.Hand;
        }

        private void CompareImage_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_comparePanning) return;
            var pos = e.GetPosition(CompareScrollViewer);
            double dx = _comparePanStart.X - pos.X;
            double dy = _comparePanStart.Y - pos.Y;
            CompareScrollViewer.ScrollToHorizontalOffset(_compareScrollStartX + dx);
            CompareScrollViewer.ScrollToVerticalOffset(_compareScrollStartY + dy);
        }

        private void CompareImage_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _comparePanning = false;
            CompareZoomContainer.ReleaseMouseCapture();
            CompareZoomContainer.Cursor = Cursors.Arrow;
        }

        private void CompareImage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_comparePanning)
            {
                _comparePanning = false;
                CompareZoomContainer.ReleaseMouseCapture();
                CompareZoomContainer.Cursor = Cursors.Arrow;
            }
        }

        #endregion
    }
}
