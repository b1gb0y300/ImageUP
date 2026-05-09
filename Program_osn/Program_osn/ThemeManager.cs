using System.Windows;
using System.Windows.Media;

namespace ImageEnhancementWpf
{
    public static class ThemeManager
    {
        public static bool IsLightTheme => AppSettings.Current.IsLightTheme;

        public static event Action? ThemeChanged;

        public static void Apply()
        {
            if (IsLightTheme) ApplyLight();
            else              ApplyDark();
            ThemeChanged?.Invoke();
        }

        public static void SetTheme(bool isLight)
        {
            AppSettings.Current.IsLightTheme = isLight;
            AppSettings.Current.Save();
            Apply();
        }

        private static Color C(string hex) =>
            (Color)ColorConverter.ConvertFromString(hex);
        private static SolidColorBrush B(string hex) =>
            new SolidColorBrush(C(hex));

        public static void ApplyDark()
        {
            var r = Application.Current.Resources;

            r["SurfaceDark"]     = C("#0F0F14");
            r["SurfaceCard"]     = C("#16161E");
            r["SurfaceElevated"] = C("#1C1C26");
            r["BorderSubtle"]    = C("#2A2A38");
            r["TextPrimary"]     = C("#F4F4F5");
            r["TextSecondary"]   = C("#A1A1AA");
            r["TextMuted"]       = C("#71717A");

            r["AccentBrush"]        = B("#6366F1");
            r["AccentBrushHover"]   = B("#818CF8");
            r["CardBrush"]          = B("#16161E");
            r["DarkPanelBrush"]     = B("#1C1C26");
            r["BorderSubtleBrush"]  = B("#2A2A38");
            r["TextPrimaryBrush"]   = B("#F4F4F5");
            r["TextSecondaryBrush"] = B("#A1A1AA");
            r["TextMutedBrush"]     = B("#71717A");

            r["WindowBackgroundStartColor"] = C("#0F0F14");
            r["WindowBackgroundEndColor"]   = C("#0A0A0E");
            r["HeaderBrush"]         = B("#16161E");
            r["LeftPanelBrush"]      = B("#12121A");
            r["SurfaceInputBrush"]   = B("#1A1A24");
            r["ImageViewerBrush"]    = B("#0C0C10");
            r["TabActiveBrush"]        = B("#22222E");
            r["TabInactiveBrush"]      = B("#0F0F16");
            r["SelectedMethodBrush"]   = B("#1E1C3A");
            r["TextSecondary2Brush"] = B("#E4E4E7");
            r["GhostButtonBg"]       = B("#252532");
            r["LoadingCardBrush"]    = B("#1C1C26");
            r["TextBoxBackgroundBrush"] = B("#111118");
            r["ComboBoxBackgroundBrush"] = B("#1E1E28");
        }

        public static void ApplyLight()
        {
            var r = Application.Current.Resources;

            r["SurfaceDark"]     = C("#F8F9FA");
            r["SurfaceCard"]     = C("#FFFFFF");
            r["SurfaceElevated"] = C("#F1F3F5");
            r["BorderSubtle"]    = C("#E2E8F0");
            r["TextPrimary"]     = C("#111827");
            r["TextSecondary"]   = C("#374151");
            r["TextMuted"]       = C("#6B7280");

            r["AccentBrush"]        = B("#6366F1");
            r["AccentBrushHover"]   = B("#818CF8");
            r["CardBrush"]          = B("#FFFFFF");
            r["DarkPanelBrush"]     = B("#F1F3F5");
            r["BorderSubtleBrush"]  = B("#E2E8F0");
            r["TextPrimaryBrush"]   = B("#111827");
            r["TextSecondaryBrush"] = B("#374151");
            r["TextMutedBrush"]     = B("#6B7280");

            r["WindowBackgroundStartColor"] = C("#F8F9FA");
            r["WindowBackgroundEndColor"]   = C("#F1F3F5");
            r["HeaderBrush"]         = B("#FFFFFF");
            r["LeftPanelBrush"]      = B("#F8F9FA");
            r["SurfaceInputBrush"]   = B("#EDF0F4");
            r["ImageViewerBrush"]    = B("#E5E9EE");
            r["TabActiveBrush"]        = B("#FFFFFF");
            r["TabInactiveBrush"]      = B("#E4E8EF");
            r["SelectedMethodBrush"]   = B("#EEF0FF");
            r["TextSecondary2Brush"] = B("#374151");
            r["GhostButtonBg"]       = B("#EDF0F4");
            r["LoadingCardBrush"]    = B("#FFFFFF");
            r["TextBoxBackgroundBrush"]  = B("#FFFFFF");
            r["ComboBoxBackgroundBrush"] = B("#F8F9FA");
        }
    }
}
