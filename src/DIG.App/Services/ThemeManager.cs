using System.Windows;

namespace DIG.App.Services;

/// <summary>Swaps the active theme ResourceDictionary (Dark/Light) at runtime.</summary>
public static class ThemeManager
{
    private const string DarkUri = "pack://application:,,,/Themes/Dark.xaml";
    private const string LightUri = "pack://application:,,,/Themes/Light.xaml";

    public static void Apply(AppTheme theme)
    {
        var effective = theme == AppTheme.System ? DetectSystem() : theme;
        var uri = effective == AppTheme.Light ? LightUri : DarkUri;

        var dicts = Application.Current.Resources.MergedDictionaries;
        // Remove any previously-applied theme dictionary.
        for (var i = dicts.Count - 1; i >= 0; i--)
        {
            var src = dicts[i].Source?.ToString();
            if (src is DarkUri or LightUri)
                dicts.RemoveAt(i);
        }
        dicts.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Absolute) });
    }

    private static AppTheme DetectSystem()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 0 ? AppTheme.Dark : AppTheme.Light;
        }
        catch
        {
            // ignore and fall back to dark
        }
        return AppTheme.Dark;
    }
}
