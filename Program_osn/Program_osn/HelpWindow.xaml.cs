using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImageEnhancementWpf
{
    public partial class HelpWindow : Window
    {
        public HelpWindow()
        {
            InitializeComponent();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ── Навигационные обработчики ─────────────────────────────────

        private void ScrollToSection(string sectionHeaderText)
        {
            // Ищем TextBlock с нужным текстом внутри ContentScroller
            if (ContentScroller.Content is not Border outerBorder) return;
            if (outerBorder.Child is not StackPanel sp) return;
            foreach (UIElement child in sp.Children)
            {
                if (child is TextBlock tb && tb.Text == sectionHeaderText)
                {
                    tb.BringIntoView();
                    return;
                }
            }
        }

        private void SetNavActive(Border active)
        {
            var navBorders = new[] { NavPurpose, NavFeatures, NavHotkeys, NavScenarios, NavMetrics };
            foreach (var b in navBorders)
            {
                b.Background = b == active
                    ? (Brush)FindResource("SurfaceInputBrush")
                    : Brushes.Transparent;
                if (b.Child is TextBlock tb)
                    tb.Foreground = b == active
                        ? (Brush)FindResource("TextPrimaryBrush")
                        : (Brush)FindResource("TextSecondaryBrush");
            }
        }

        private void NavPurpose_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { SetNavActive(NavPurpose); ScrollToSection("Назначение приложения"); }

        private void NavFeatures_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { SetNavActive(NavFeatures); ScrollToSection("Ключевые возможности"); }

        private void NavHotkeys_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { SetNavActive(NavHotkeys); ScrollToSection("Горячие клавиши"); }

        private void NavScenarios_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { SetNavActive(NavScenarios); ScrollToSection("Сценарии использования"); }

        private void NavMetrics_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        { SetNavActive(NavMetrics); ScrollToSection("Метрики качества"); }
    }
}
