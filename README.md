# D!G! — Infrastructure Browser

> One workspace. Every tool. Less friction.

D!G! (pronounced "Dig") is a Windows desktop **infrastructure-focused web browser** for
VMware support engineers, network/system administrators, and cybersecurity and IT
professionals. It combines a real Chromium-based browser (Microsoft Edge WebView2) with
infrastructure intelligence, a VMware build lookup, secure technical reference management,
and **Google Gemini**–powered technical writing — in one coherent app.

This repository contains the **source, build scripts, automated tests, packaging, and
documentation**. A Windows CI workflow builds the standalone `D!G!.exe` and an installer.

---

## ⚠️ Status — read this first

This is an **early, honest foundation**, not a finished 21-section product. It is built in
increments and ships a **feature-completion matrix** (`docs/FEATURE_MATRIX.md`) that marks
every capability as **implemented, partial, or planned**. Modules that are not yet built
show a clear "planned" placeholder in the app — never a fake button.

**What works today**

- A real tabbed browser (WebView2): navigation, omnibox, configurable search engines.
- A **smart omnibox** that recognises URLs vs. searches vs. IPs / FQDNs / VMware build numbers.
- **Right-click selection actions** on any page: Explain / Summarize (Gemini), Identify VMware
  build, Draft email from selection.
- **Settings → AI**: paste your **Gemini API key** (stored encrypted via Windows DPAPI),
  pick a model, test the connection.
- **AI Assistant** chat (Gemini), with credential redaction before anything is sent.
- **Email Templates**: user-editable templates + AI drafting that fills a template for the
  current case (drafts only — nothing is ever sent automatically).
- **VMware Build Lookup** with honest Verified / Possible / Unknown confidence.
- **Bookmarks**, **History**, **Command palette** (Ctrl+K), **dark/light themes**.
- SQLite persistence with versioned migrations; structured logging.

See `docs/FEATURE_MATRIX.md` for the full, honest breakdown and `docs/KNOWN_LIMITATIONS.md`.

---

## How to get `D!G!.exe`

You cannot build a Windows WPF app on Linux/macOS. Use **one** of these:

### Option A — download from CI (recommended)
Every push runs `.github/workflows/windows-build.yml` on a Windows runner, which:
1. builds the solution, 2. runs the tests, 3. publishes a **self-contained single-file**
`D!G!.exe` (no .NET install needed by end users), 4. builds an **EXE installer**.

Download them from the workflow run's **Artifacts** (`DIG-app-*` and `DIG-installer-*`).

### Option B — build locally on Windows
Requirements: Windows 10/11 x64, [.NET 8 SDK](https://dotnet.microsoft.com/download),
and the [Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)
(present on Windows 11 and most Windows 10).

```powershell
# from the repo root, in PowerShell
./build/build.ps1                 # restore + build + test
./build/publish.ps1 -Version 0.1.0   # -> artifacts/app/D!G!.exe  (self-contained)
```

Build the installer (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DMyAppVersion=0.1.0 `
  /DSourceDir=(Resolve-Path artifacts/app) build/installer/DIG.iss
# -> build/installer/Output/DIG-Setup-0.1.0.exe
```

---

## Set up Gemini AI

1. Create an API key in **Google AI Studio**.
2. Open D!G! → **Settings → AI**, paste the key, click **Save key**, then **Test**.
3. The key is encrypted with **Windows DPAPI** (per-user) and is **never** written to the
   database, logs, bookmarks or AI prompts. Clearing it removes it immediately.

Before any text is sent to Gemini, D!G! **redacts credentials** (API keys, tokens, passwords,
private keys, URL credentials). You can additionally redact IPs/hostnames in Settings.

---

## Architecture (short)

| Project       | Target            | Purpose                                                        |
|---------------|-------------------|----------------------------------------------------------------|
| `DIG.Core`    | `net8.0`          | Models, intelligence (identifier detection, build lookup), Gemini client, redaction, SQLite data layer. Cross-platform, fully unit-tested. |
| `DIG.App`     | `net8.0-windows`  | WPF UI (MVVM), WebView2 browser host, DPAPI secret store, theming. |
| `DIG.Tests`   | `net8.0`          | xUnit tests for all `DIG.Core` logic.                          |

More in `docs/ARCHITECTURE.md`. Security model in `docs/SECURITY.md`.

## Repository layout

```
src/DIG.Core      core logic + data (testable anywhere)
src/DIG.App       WPF application (Windows)
tests/DIG.Tests   xUnit tests
build/            build + publish scripts, Inno Setup installer
.github/workflows CI that builds D!G!.exe + installer on Windows
docs/             architecture, security, user guide, feature matrix, limitations
assets/branding   logo / icon / design tokens
```

## License

See `LICENSE`. Third-party dependencies and their licenses: `docs/THIRD_PARTY_NOTICES.md`.
