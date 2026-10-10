namespace DIG.App.Services;

/// <summary>A selectable look: base light/dark, an accent colour, and a 3-stop
/// background gradient used behind the start page.</summary>
public sealed record ThemePreset(
    string Id,
    string Name,
    bool IsDark,
    string AccentHex,
    string Grad1,
    string Grad2,
    string Grad3);

public static class ThemePresets
{
    public static readonly IReadOnlyList<ThemePreset> All = new[]
    {
        new ThemePreset("obsidian", "Obsidian (dark)", true,  "#35D9FF", "#0E1116", "#141C2B", "#0F1620"),
        new ThemePreset("midnight", "Midnight Blue",    true,  "#4D8DFF", "#0A1020", "#122445", "#0B1630"),
        new ThemePreset("violet",   "Violet Night",     true,  "#9B8CFF", "#120F20", "#281C46", "#160F2A"),
        new ThemePreset("aurora",   "Aurora",           true,  "#35FFC2", "#06140F", "#0C3030", "#0A2238"),
        new ThemePreset("sakura",   "Sakura (anime)",   true,  "#FF8FB3", "#1A0F15", "#3C1A33", "#2A1228"),
        new ThemePreset("ember",    "Ember",            true,  "#FF9F45", "#160F0C", "#351B12", "#24130E"),
        new ThemePreset("daylight", "Daylight (light)", false, "#0E9EC9", "#F5F7FA", "#E6EEF6", "#EEF2F8"),
    };

    public static ThemePreset ById(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}
