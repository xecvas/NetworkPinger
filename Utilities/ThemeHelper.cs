using System.Windows;
using System.Windows.Media;

namespace Network_Pinger.Utilities
{
    // Centralized utility for dynamic UI theme management
    public static class ThemeHelper
    {
        private static readonly BrushConverter BrushConverter = new BrushConverter();

        // Apply light/dark theme brushes to a resource dictionary
        public static void ApplyTheme(ResourceDictionary res, bool isDark, bool isMain)
        {
            res["CardBgBrush"] = GetBrush(isDark ? "#2D2D30" : "#FFFFFF");
            res["TextPrimaryBrush"] = GetBrush(isDark ? "#FFFFFF" : "#212529");
            res["TextSecondaryBrush"] = GetBrush(isDark ? "#CCCCCC" : "#495057");
            res["BorderDefaultBrush"] = GetBrush(isDark ? "#434346" : "#DEE2E6");
            res["ControlBgBrush"] = GetBrush(isDark ? "#3E3E42" : "#E9ECEF");

            if (isMain)
                res["AppBgBrush"] = GetBrush(isDark ? "#202020" : "#F0F2F5");
        }

        // Convert a hex color string to a SolidColorBrush
        public static SolidColorBrush GetBrush(string hex) =>
            (SolidColorBrush)BrushConverter.ConvertFromString(hex);
    }
}