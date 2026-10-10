# D!G! Architecture

## Overview

D!G! is a WPF (.NET 8) desktop application using **MVVM**, dependency injection, and an
embedded Chromium browser via **Microsoft Edge WebView2**. Logic that can be tested without
Windows lives in a separate, cross-platform `DIG.Core` assembly.

```
┌───────────────────────────────────────────────┐
│ DIG.App (net8.0-windows, WPF)                   │
│  Views (MainWindow + typed DataTemplates)       │
│  ViewModels (CommunityToolkit.Mvvm)             │
│  Services: DPAPI secret store, AppSettings,     │
│            AiService, ThemeManager, paths       │
│  WebView2 host (code-behind, IBrowserController)│
└───────────────┬─────────────────────────────────┘
                │ depends on
┌───────────────▼─────────────────────────────────┐
│ DIG.Core (net8.0, no Windows/WPF)                │
│  Models, Intelligence (identifier detection,     │
│  omnibox interpreter, VMware build lookup),      │
│  Ai (IAiProvider + GeminiAiProvider),            │
│  Security (redaction, ISecretStore),             │
│  Email (templates + renderer),                   │
│  Data (SQLite factory, migrator, repositories)   │
└──────────────────────────────────────────────────┘
      ▲
      │ tested by
┌─────┴───────────────┐
│ DIG.Tests (xUnit)   │
└─────────────────────┘
```

## Key decisions

- **MVVM for tool modules**: each sidebar tool is a ViewModel rendered by a typed
  `DataTemplate` in `Themes/ModuleTemplates.xaml`. No per-view code-behind required.
- **WebView2 in code-behind**: a browser control is inherently view-bound, so the
  `MainWindow` owns the WebView2 instances and implements `IBrowserController`, which the
  `MainViewModel` calls. This is the smallest pragmatic exception to strict MVVM.
- **Provider-neutral AI**: `IAiProvider` abstracts the model. `GeminiAiProvider` is stateless
  (HttpClient + per-request key), which makes it unit-testable with a stub `HttpMessageHandler`.
- **Secrets are separate from data**: `ISecretStore` (DPAPI on Windows) holds the Gemini key;
  the SQLite database holds only non-secret settings and metadata.
- **Source-adapter build lookup**: `IBuildSource` lets the bundled offline index and a future
  authoritative online source coexist; confidence reflects which answered.
- **Versioned schema**: `DatabaseMigrator` applies an ordered list of SQL migrations tracked by
  SQLite `PRAGMA user_version`; adding a migration is appending one string.

## Data locations (Windows)

```
%LOCALAPPDATA%\DIG\dig.db          SQLite database
%LOCALAPPDATA%\DIG\secrets.dat     DPAPI-encrypted secrets (per-user)
%LOCALAPPDATA%\DIG\logs\           Serilog rolling logs
%LOCALAPPDATA%\DIG\WebView2\       WebView2 user data folder
%LOCALAPPDATA%\DIG\backups\        reserved for backups
```

## Threading

UI runs on the WPF dispatcher. AI/network calls are `async` and awaited from commands.
WebView2 events arrive on the UI thread. A `DispatcherTimer` updates the status-bar clocks.

## Testing

`DIG.Core` is covered by xUnit tests (identifier detection, omnibox interpretation, build
lookup confidence, redaction, Gemini payload/response handling, email rendering, SQLite
migrations and repositories). CI runs them on every push before publishing.
