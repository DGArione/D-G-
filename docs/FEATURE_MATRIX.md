# D!G! Feature-completion matrix

Legend: ✅ Implemented · 🟡 Partial · ⬜ Planned (shown as an honest placeholder in-app)

This matrix maps the master spec to what actually exists in the codebase today. It is
updated with each increment and is the source of truth over any marketing copy.

## Browser (spec §6)
| Feature | Status | Notes |
|---|---|---|
| Real navigation/rendering (WebView2) | ✅ | Chromium via Edge WebView2 |
| Back/forward/reload/stop/home | ✅ | |
| Omnibox (URL vs search) | ✅ | Smart: detects IP/FQDN/build numbers |
| Configurable search engine (Google/DuckDuckGo/Brave) | ✅ | Settings |
| Tabs, new/close/activate, restore active | 🟡 | Tabs work; session restore across restarts planned |
| Bookmarks | ✅ | add/open/delete, SQLite |
| History | ✅ | recorded, view, clear |
| Downloads manager | ⬜ | planned |
| Pinned tabs / tab groups / split view | ⬜ | planned |
| Private/profile isolation | ⬜ | planned (do not treat as anonymity) |
| Ad/tracker blocker | ⬜ | planned (needs licensed rules engine) |
| Find in page / reader / translate page | ⬜ | planned |
| TLS validation (no silent bypass) | ✅ | WebView2 default; we never disable it |

## Context-aware actions (spec §7)
| Feature | Status | Notes |
|---|---|---|
| Selection right-click actions | ✅ | Explain, Summarize, Identify build, Draft email |
| Identifier detection (IP/IPv6/FQDN/build/version/error/k8s/timestamp) | ✅ | `TechnicalIdentifierDetector`, unit-tested |

## VMware build intelligence (spec §8)
| Feature | Status | Notes |
|---|---|---|
| Build Lookup module | ✅ | normalize, lookup, confidence |
| Source-adapter architecture | ✅ | `IBuildSource` (bundled + authoritative slot) |
| Verified/Possible/Unknown confidence | ✅ | bundled data is never "Verified" |
| Bundled offline index | ✅ | curated ESXi + vCenter GA/update builds (embedded JSON) |
| Import from Broadcom page (browser) | ✅ | scrapes the rendered KB table from the active tab → Verified, cached |
| Import from CSV | ✅ | load the exact vendor table yourself |
| Live authoritative source | ✅ | via browser-import (SPA-safe); server-side scrape not used |
| Recent lookups | ✅ | in session; pinning/persistent cache planned |

## AI (spec §12)
| Feature | Status | Notes |
|---|---|---|
| Gemini provider (REST) | ✅ | `GeminiAiProvider`, unit-tested |
| Provider abstraction | ✅ | `IAiProvider` |
| API key in Settings, encrypted | ✅ | DPAPI; never logged/DB/prompts |
| Redaction before sending | ✅ | credentials always; IP/FQDN optional |
| AI chat assistant | ✅ | |
| Email templates (user-editable) | ✅ | CRUD + built-ins |
| AI draft from template for current case | ✅ | drafts only; never auto-sends |
| Reply using pasted email thread (your + customer emails) | ✅ | AI uses the conversation as context |
| Writing tones | ✅ | professional/concise/customer-friendly/technical |
| Local model option / Outlook integration | ⬜ | planned |

## Infrastructure, utilities, remote (spec §9–11)
| Feature | Status | Notes |
|---|---|---|
| Infrastructure inventory | ⬜ | schema present (`infrastructure_assets`); UI planned |
| Secure vault (DPAPI) foundation | ✅ | `ISecretStore` + DPAPI used for API key |
| Hosts-file manager | ⬜ | planned (privileged helper required) |
| SSH/SFTP/RDP launchers | ⬜ | planned |

## Calendar, productivity, workspaces, KB (spec §13–15)
| Feature | Status | Notes |
|---|---|---|
| Calendar / shift import / ICS | ⬜ | planned |
| Tasks & notes | ⬜ | planned |
| Workspaces | ⬜ | planned |
| Knowledge base / full-text search | ⬜ | planned |
| Time-zone clocks (Colombo/UTC) | ✅ | status bar |

## Platform (spec §2,16,17,18,19)
| Feature | Status | Notes |
|---|---|---|
| Settings (theme/search/homepage/AI/privacy toggle) | 🟡 | core set done; more planned |
| Dark/light/system themes | ✅ | runtime switch |
| SQLite + versioned migrations | ✅ | `DatabaseMigrator` (PRAGMA user_version) |
| Structured logging (no secrets) | ✅ | Serilog to `%LOCALAPPDATA%\DIG\logs` |
| Command palette (Ctrl+K) | ✅ | |
| Self-contained `D!G!.exe` | ✅ | via `publish.ps1` / CI |
| Installer (EXE) | ✅ | Inno Setup, per-user, no admin |
| Automated tests | ✅ | xUnit over `DIG.Core` |
| Signed release build | ⬜ | wired when a code-signing cert is provided |
| Update mechanism | ⬜ | planned |
