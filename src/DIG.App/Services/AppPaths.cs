using System.IO;

namespace DIG.App.Services;

/// <summary>Resolves the Windows app-data locations D!G! stores user data in.</summary>
public static class AppPaths
{
    public const string AppFolderName = "DIG";

    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string DatabasePath => Path.Combine(DataDir, "dig.db");
    public static string LogsDir => Path.Combine(DataDir, "logs");
    public static string WebView2Dir => Path.Combine(DataDir, "WebView2");
    public static string BackupsDir => Path.Combine(DataDir, "backups");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(WebView2Dir);
        Directory.CreateDirectory(BackupsDir);
    }
}
