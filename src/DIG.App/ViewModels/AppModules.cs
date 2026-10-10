using CommunityToolkit.Mvvm.ComponentModel;

namespace DIG.App.ViewModels;

public enum ModuleId
{
    Browser,
    Workspaces,
    Infrastructure,
    BuildLookup,
    KnowledgeBase,
    AiAssistant,
    EmailTemplates,
    Calendar,
    Tasks,
    Ssh,
    Downloads,
    Bookmarks,
    History,
    Settings
}

/// <summary>A sidebar entry. Glyphs use the Segoe MDL2 Assets font.</summary>
public sealed partial class SidebarItemViewModel : ObservableObject
{
    public SidebarItemViewModel(ModuleId id, string label, string glyph, bool implemented)
    {
        Id = id;
        Label = label;
        Glyph = glyph;
        Implemented = implemented;
    }

    public ModuleId Id { get; }
    public string Label { get; }
    public string Glyph { get; }
    public bool Implemented { get; }

    [ObservableProperty]
    private bool _isActive;
}
