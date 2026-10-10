# D!G! Design tokens

The canonical token values live in [`assets/branding/design-tokens.json`](../../assets/branding/design-tokens.json)
and are mirrored in the WPF theme dictionaries `src/DIG.App/Themes/Dark.xaml` and `Light.xaml`.
Keep all three in sync when changing the palette.

## Colour (dark — primary)
| Token | Hex | Use |
|---|---|---|
| Obsidian | `#101318` | window background, title bar |
| Graphite | `#191E26` | sidebar, toolbars |
| Surface | `#222936` | panels, cards, menus |
| Surface Alt | `#2A3240` | hover / active surface |
| Border | `#303946` | dividers, borders |
| Text Primary | `#F4F7FB` | primary text |
| Text Secondary | `#A7B1C2` | metadata, labels |
| Electric Cyan | `#35D9FF` | active states, focus, key actions |
| Signal Violet | `#8B7CFF` | AI / advanced features (sparingly) |
| Success | `#36D399` | success status |
| Warning | `#F5B942` | warning status |
| Error | `#FF647C` | error status |

Status is never communicated by colour alone — always pair with a label or icon.

## Typography
- **UI**: Segoe UI Variable Text (fallback Segoe UI).
- **Technical/mono**: Cascadia Code (fallback Consolas) for IPs, FQDNs, build numbers, logs.

## Shape, spacing, motion
- Radius: control 6px, card 10px, dialog 12px.
- Spacing scale (4px base): 4 / 8 / 12 / 16 / 24 / 32.
- Motion: 120–160ms; respect reduced-motion.

## Logo
- Wordmark `D!G!` with **cyan exclamation marks** (part of the identity, never removed).
- `assets/branding/logo.svg` — wordmark + descriptor lockup.
- `assets/branding/icon.svg` — app-icon mark (vertical bar + dot, echoing the "!").
- `assets/branding/dig.ico` / `src/DIG.App/Assets/dig.ico` — multi-resolution app icon.
