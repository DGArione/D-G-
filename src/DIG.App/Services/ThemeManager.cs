using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DIG.App.Services;

/// <summary>Applies a theme preset at runtime: swaps the base Dark/Light dictionary,
/// overrides the accent colour, and sets the start-page background (gradient or a
/// user-chosen image).</summary>
public static class ThemeManager
{
    private const string DarkUri = "pack://application:,,,/Themes/Dark.xaml";
    private const string LightUri = "pack://application:,,,/Themes/Light.xaml";

    public static void ApplyFromSettings(AppSettingsService settings)
        => Apply(ThemePresets.ById(settings.ThemePresetId), settings.CustomBackgroundPath);

    public static void Apply(ThemePreset preset, string? customBackgroundPath)
    {
        var app = Application.Current;
        if (app is null) return;

        // 1. Base dictionary (dark/light).
        var uri = preset.IsDark ? DarkUri : LightUri;
        var dicts = app.Resources.MergedDictionaries;
        for (var i = dicts.Count - 1; i >= 0; i--)
        {
            var src = dicts[i].Source?.ToString();
            if (src is DarkUri or LightUri) dicts.RemoveAt(i);
        }
        dicts.Insert(0, new ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) });

        // 2. Accent override (app-level resources win over the merged dictionary).
        var accent = ParseColor(preset.AccentHex, Color.FromRgb(0x35, 0xD9, 0xFF));
        app.Resources["Color.Cyan"] = accent;
        app.Resources["Brush.Cyan"] = new SolidColorBrush(accent);

        // 3. Start-page background: custom image if set & readable, else the gradient.
        app.Resources["Brush.StartBackground"] = BuildBackground(preset, customBackgroundPath);
    }

    private static Brush BuildBackground(ThemePreset preset, string? customBackgroundPath)
    {
        if (!string.IsNullOrWhiteSpace(customBackgroundPath) && File.Exists(customBackgroundPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(customBackgroundPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
            }
            catch
            {
                // fall through to gradient
            }
        }

        var g = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        g.GradientStops.Add(new GradientStop(ParseColor(preset.Grad1, Colors.Black), 0.0));
        g.GradientStops.Add(new GradientStop(ParseColor(preset.Grad2, Colors.Black), 0.55));
        g.GradientStops.Add(new GradientStop(ParseColor(preset.Grad3, Colors.Black), 1.0));
        g.Freeze();
        return g;
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex)!; }
        catch { return fallback; }
    }
}
