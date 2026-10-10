# D!G! Configuration & Backup

## Configuration
Most configuration is in-app (**Settings**). Values are stored in the SQLite `settings` table
at `%LOCALAPPDATA%\DIG\dig.db`. The Gemini API key is stored separately, encrypted, in
`%LOCALAPPDATA%\DIG\secrets.dat` (DPAPI, current user only).

| Setting | Key | Default |
|---|---|---|
| Theme | `theme` | Dark |
| Search engine | `search_engine` | DuckDuckGo |
| Homepage | `homepage` | https://duckduckgo.com |
| AI enabled | `ai_enabled` | true |
| Gemini model | `gemini_model` | gemini-1.5-flash |
| AI temperature | `ai_temperature` | 0.3 |
| Redact IP/FQDN before AI | `ai_redact_network` | false |

## Backup
To back up your D!G! data, copy the whole folder:

```
%LOCALAPPDATA%\DIG\
```

- `dig.db` (+ `dig.db-wal`, `dig.db-shm`) — settings, bookmarks, history, email templates.
- `secrets.dat` — encrypted secrets. **Note:** DPAPI secrets are tied to the Windows user
  account; restoring `secrets.dat` to a different user/machine will not decrypt. Re-enter the
  Gemini API key there instead.

Close D!G! before backing up to ensure the SQLite WAL is checkpointed.

## Restore
Copy the folder back to `%LOCALAPPDATA%\DIG\` on the same Windows user account, then start D!G!.

## Upgrades & uninstall
- Installing a newer build preserves `%LOCALAPPDATA%\DIG` (the installer only replaces program
  files under `%LOCALAPPDATA%\Programs\DIG`).
- Uninstalling removes the program files but **not** your data folder. Delete
  `%LOCALAPPDATA%\DIG` manually if you want to remove all user data.

## Export (planned)
Bookmark import/export and encrypted backup/restore from within the app are planned; see
`FEATURE_MATRIX.md`.
