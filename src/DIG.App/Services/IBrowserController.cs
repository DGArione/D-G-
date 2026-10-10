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

    /// <summary>Runs JavaScript in the active tab and returns the JSON-encoded result
    /// (or null if there is no active tab). Used to import data from a rendered page.</summary>
    Task<string?> ExecuteScriptOnActiveTabAsync(string script);

    /// <summary>The active tab's current URL, or null.</summary>
    string? ActiveUrl { get; }

    void Back();
    void Forward();
    void Reload();
    void Stop();
    void GoHome();
}
