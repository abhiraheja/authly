# Design Tokens

All tokens live in `assets/theme.css` as **space-separated RGB channels** (so Tailwind opacity
utilities like `bg-primary/10` work). Re-theme an entire product by editing the `:root` block.

## Brand palette (SAARVIX)
| Token | Light | Dark |
|-------|-------|------|
| `--primary` | `#6A5ACD` | `#8B7DE0` |
| `--primary-foreground` | `#FEFEFE` | `#0D0D54` |
| `--ink` | `#0D0D54` | `#0D0D54` |
| `--teal` | `#1B9BC0` | `#1B9BC0` |
| `--mint` | `#80EDE8` | `#80EDE8` |
| `--coral` | `#FF7E6B` | `#FF7E6B` |
| `--slate` | `#333444` | `#333444` |

## Semantic
| Token | Light | Dark |
|-------|-------|------|
| `--background` | `#FEFEFE` | `#0B0B40` |
| `--foreground` | `#0D0D54` | `#F5F5FC` |
| `--card` | `#FFFFFF` | `#1A1A66` |
| `--popover` | `#FFFFFF` | `#1A1A66` |
| `--secondary` | `#F1F0FB` | `#2A2A78` |
| `--muted` | `#F4F4F9` | `#24246E` |
| `--muted-foreground` | `#6B6B86` | `#B8B5DC` |
| `--accent` | `#EEEBFA` | `#2F2F85` |
| `--border` / `--input` | `#E6E5F1` | `#303078` |
| `--ring` | `#6A5ACD` | `#8B7DE0` |

## State
| Token | Value |
|-------|-------|
| `--success` | `#34C29A` |
| `--warning` | `#F4B740` |
| `--danger` | `#FF7E6B` |
| `--info` | `#1B9BC0` |

## Sidebar
`--sidebar` `#FFFFFF`/`#11115E`, `--sidebar-foreground` `#333444`/`#E8E7F5`, `--sidebar-accent`,
`--sidebar-border`. Sidebar is light by default in both themes' chrome.

## Chart series
`--chart-1`=primary, `--chart-2`=teal, `--chart-3`=mint, `--chart-4`=coral, `--chart-5`=warning.

## Radius (tight, enterprise — never exceed 8px)
| Token | Value | Use |
|-------|-------|-----|
| `--radius` | `8px` | cards, menus, popovers |
| `--radius-control` | `6px` | buttons, inputs, segmented |
| `--radius-chip` | `4px` | small buttons, chips, kbd |

`tw-config.js` also remaps Tailwind's `rounded-*` scale so inline `rounded-lg`(6) / `rounded-xl`(8) /
`rounded-2xl`(8) stay ≤8px. Avatars/pills use `rounded-full`.

## Elevation (shadows)
`--shadow-sm`, `--shadow` (DEFAULT), `--shadow-md`, `--shadow-lg` — low-opacity, indigo-tinted in
light, black in dark. Use Tailwind `shadow-sm|shadow|shadow-md|shadow-lg`.

## Typography
- Family: `Inter` with native SF-Pro stack first (`-apple-system, BlinkMacSystemFont, "SF Pro Display", "Inter", …`).
- Headings use `.font-display` (same family) + `letter-spacing:-0.018em`.
- Scale (Tailwind): display `text-3xl/4xl font-bold`, h1 `text-2xl`, h2 `text-xl`, h3 `text-lg`,
  body `text-sm` (default UI size), caption `text-xs text-muted-foreground`.
- Weights loaded: 400, 500, 600, 700, 800.

## Usage rules
- Reference tokens via Tailwind: `bg-primary`, `text-muted-foreground`, `border-border`, `bg-primary/10`.
- In raw CSS/inline styles use `rgb(var(--token))` or `rgb(var(--token) / .5)`.
- Never hard-code hex except gradient utilities (`from-primary to-teal`) and real third-party brand colors.
- Dark mode is automatic when you only use tokens — never set fixed light backgrounds/text.
