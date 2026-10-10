# Third-party dependencies & licenses

D!G! uses the following third-party packages. Verify license terms before any commercial
distribution.

| Package | Purpose | License |
|---|---|---|
| Microsoft.Web.WebView2 | Embedded Chromium browser control | Microsoft Software License (WebView2 SDK) |
| Microsoft.Data.Sqlite | SQLite data access | MIT |
| CommunityToolkit.Mvvm | MVVM (ObservableObject, RelayCommand) | MIT |
| Microsoft.Extensions.DependencyInjection | Dependency injection | MIT |
| Microsoft.Extensions.Hosting / Logging | Hosting + logging abstractions | MIT |
| Serilog, Serilog.Extensions.Logging, Serilog.Sinks.File | Structured logging | Apache-2.0 |
| System.Security.Cryptography.ProtectedData | DPAPI secret encryption | MIT |
| xUnit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk | Unit testing | Apache-2.0 / MIT |

Runtime components the end user must have:
- **Microsoft Edge WebView2 Runtime** (Microsoft terms) — for browsing.

Build/packaging tools (not distributed in the app):
- **.NET 8 SDK** (MIT / .NET Library License)
- **Inno Setup** (Inno Setup license — free for any use incl. commercial)

External services:
- **Google Gemini API** — subject to Google's API terms; the user supplies their own key.

VMware/Broadcom product names and build numbers are trademarks/data of their respective
owners; the bundled reference index is for convenience and must be verified against official
Broadcom documentation.
