# D!G! Release notes

## 0.1.0 — Foundation (unreleased)

First functional foundation of the D!G! Infrastructure Browser.

**Added**
- Tabbed Chromium browser (Edge WebView2): navigation, stop/reload/home, new/close tabs.
- Smart omnibox: distinguishes URLs, bare domains, IPs/`localhost`, and searches; recognises
  VMware build numbers / FQDNs / IPs and surfaces them.
- Right-click selection actions on web pages: Explain, Summarize (Gemini), Identify VMware
  build, Draft email from selection.
- Google Gemini integration: provider abstraction + REST client; API key stored encrypted
  (Windows DPAPI); credential redaction before sending; AI Assistant chat.
- Email Templates: built-in + user-editable templates with `{{variables}}`; local fill and
  AI drafting for the current case (drafts only, never auto-sent); tone selection.
- VMware Build Lookup with Verified/Possible/Unknown confidence and a source-adapter design.
- Bookmarks, History, Command palette (Ctrl+K), dark/light/system themes, status-bar clocks.
- SQLite persistence with versioned migrations; Serilog logging (no secrets).
- Self-contained single-file `D!G!.exe` publish; Inno Setup per-user installer; Windows CI.
- xUnit test suite over `DIG.Core`.

**Known limitations**: see `KNOWN_LIMITATIONS.md` and `FEATURE_MATRIX.md`.
