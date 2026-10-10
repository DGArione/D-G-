using DIG.App.ViewModels;

namespace DIG.App.Services;

/// <summary>
/// Implemented by the main window to let view-models drive the WebView2-backed
/// browser without taking a direct dependency on the view. WebView2 is inherently
/// view-bound, so the actual control management lives in the window code-behind.
/// </summary>
public interface IBrowserController
{
    void NewTab(string? url = null, bool activate = true);
    void CloseTab(BrowserTabViewModel tab);
    void Activate(BrowserTabViewModel tab);

    /// <summary>Interprets omnibox text (URL vs search) and navigates the active tab.</summary>
    void NavigateFromOmnibox(string text);

    void NavigateActive(string url);
    void Back();
    void Forward();
    void Reload();
    void Stop();
    void GoHome();
}
