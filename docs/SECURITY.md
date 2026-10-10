# D!G! Security & Privacy

Security is a primary product requirement. This describes what the current build does and is
honest about what is not yet implemented. D!G! never claims "100% secure" or anonymity.

## Credentials & secrets
- The Gemini API key is stored with **Windows DPAPI** (`DataProtectionScope.CurrentUser`) in
  `%LOCALAPPDATA%\DIG\secrets.dat`. Only the current Windows user can decrypt it.
- Secrets are **never** written to the SQLite database, logs, bookmarks, exports, or AI prompts.
- The abstraction is `DIG.Core.Security.ISecretStore`; the Windows implementation is
  `DpapiSecretStore`. A non-persistent in-memory fallback exists for tests/non-Windows.

## Sending data to AI (explicit, redacted)
- Before any text is sent to Gemini, `SensitiveDataRedactor` removes: private keys, Google API
  keys, AWS access keys, JWTs, bearer tokens, `password=`/`token=`-style assignments, and
  credentials embedded in URLs.
- Settings → AI has an optional toggle to also redact **IP addresses and hostnames**
  (customer-sensitive) before sending.
- AI can be **disabled entirely** in Settings. No telemetry is sent anywhere by D!G! itself.
- AI output is always a **draft for review**. Emails are **never sent automatically**.

## Web content & process boundaries
- Browsing uses Microsoft Edge WebView2. Web page JavaScript has **no bridge** to native APIs,
  the filesystem, secrets, or shell execution. The only native affordances exposed to a page are
  a few context-menu actions that read the current **selection text** and route it to D!G!
  features — they do not grant the page any capability.
- TLS certificate validation is **left to WebView2's defaults and is never silently bypassed**.

## Input handling
- Omnibox input is classified (URL vs search) and only navigated when it is a valid URL;
  otherwise it becomes a search query. URLs are normalised to an explicit scheme.
- SQLite access uses **parameterised queries** throughout (no string concatenation).

## Logging
- Structured logging (Serilog) writes to `%LOCALAPPDATA%\DIG\logs`. We log model names, HTTP
  status and error messages — **never** API keys, request bodies, or secrets.

## Not yet implemented (planned, tracked in FEATURE_MATRIX)
- Hosts-file editing via a narrow privileged helper (the browser never runs elevated).
- SSH known-host verification and key handling.
- Ad/tracker blocking, Safe-Browsing reputation integration.
- Encrypted backups, secure auto-update verification, code signing.
- Private-browsing data isolation (and it will not be described as anonymity).

## Reporting
For a real deployment, add a SECURITY contact/policy here. This is a working brand project;
treat it as pre-release software.
