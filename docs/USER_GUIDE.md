# D!G! User Guide

## First run
1. Install the Edge WebView2 Runtime if prompted (Windows 11 and most Windows 10 already have it).
2. Launch D!G!. The browser opens on your homepage (default DuckDuckGo).
3. Open **Settings → AI** and paste your **Gemini API key** to enable AI features (optional).

## Browsing
- Type in the **omnibox** and press Enter. D!G! decides:
  - a full URL or bare domain → navigates;
  - a bare IP or `localhost[:port]` → navigates (http);
  - anything else → searches with your configured engine.
- Toolbar: back, forward, reload, stop, home, new tab, bookmark, command palette.
- **Ctrl+T** new tab · **Ctrl+K** command palette.

## Smart selection actions
Select text on any page and **right-click**:
- **Explain selection** / **Summarize selection** → opens the AI Assistant with the result.
- **Identify VMware build** → runs it through the Build Lookup.
- **Draft email from selection** → opens Email Templates with the text as case context.

## VMware Build Lookup
Enter a build number (e.g. `20513097`) or version, press **Identify build**. Results are marked:
- **Verified** — from an authoritative source (when configured),
- **Possible** — from the bundled offline reference (confirm on Broadcom),
- **Unknown** — no match. D!G! never invents a product or date.

Use **Search official docs** to open a Broadcom-scoped search.

## AI Assistant
Chat with Gemini for explanations, summaries and drafting. Credentials in your text are
redacted before sending. Turn AI off anytime in Settings.

## Email Templates
- Pick a built-in template or create your own. Templates use `{{variable}}` placeholders.
- Fill the **variables for the current case** (customer, case number, product version, …).
- **Fill from template** renders locally (no AI, works offline).
- **Draft with AI** asks Gemini to produce a polished draft in your chosen tone.
- Review/edit the draft and **Copy** it. Nothing is ever sent automatically.

## Bookmarks & History
- Bookmark the current page from the toolbar. Manage in the **Bookmarks** module.
- **History** lists recent visits; open or clear them.

## Settings
- **Appearance**: dark / light / system theme.
- **Browsing**: default search engine, homepage.
- **AI**: enable/disable, model, creativity, redaction, API key (save/clear/test).

## Where your data lives
`%LOCALAPPDATA%\DIG` — database, encrypted secrets, logs, WebView2 profile, backups.
Uninstalling does **not** delete this folder; remove it manually to wipe user data.
