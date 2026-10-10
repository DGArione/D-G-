using CommunityToolkit.Mvvm.ComponentModel;

namespace DIG.App.ViewModels;

/// <summary>Honest placeholder for modules that are planned but not yet implemented
/// in this build. Shown instead of a fake, non-functional UI.</summary>
public sealed partial class PlaceholderViewModel : ObservableObject
{
    public PlaceholderViewModel(string title, string message)
    {
        Title = title;
        Message = message;
    }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _message;
}
