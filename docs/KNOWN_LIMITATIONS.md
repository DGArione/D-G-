# D!G! Known limitations

- **Windows only.** The UI is WPF + WebView2. A cross-platform build would require a different
  UI stack (e.g. Avalonia) — not in scope for this build. `DIG.Core` is already cross-platform.
- **Requires the Edge WebView2 Runtime.** Bundled on Windows 11 and most Windows 10; the
  installer warns if it is missing. D!G! does not silently install it.
- **Many modules are placeholders.** Infrastructure inventory, hosts-file manager, SSH/RDP,
  calendar/shifts, tasks/notes, workspaces, knowledge base, downloads UI, ad/tracker blocking
  and profiles are **planned** — see `FEATURE_MATRIX.md`. The app shows an honest placeholder.
- **VMware data is limited and offline.** The bundled index is a small, conservative set and is
  reported as "Possible", never "Verified". Live authoritative lookup is a documented adapter
  slot, intentionally unimplemented until an approved source/robots/licensing path exists.
- **No session restore across restarts yet**, no tab groups/pinned/split view yet.
- **AI requires your own Gemini API key** and network access. Usage is billed by Google.
- **No code signing yet.** SmartScreen may warn on first run until a signing certificate is
  wired into the release build.
- **No auto-update yet.** Update by installing a newer build; user data is preserved.
- **Private browsing is not implemented**, and when it is it will not be presented as anonymity.
- **The app cannot be built or verified on Linux/macOS**; use the Windows CI workflow or a
  Windows machine (see README).
