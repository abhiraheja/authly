# 11 — Typography & Utilities

From `ui-core` (typography) + `ui-utilities`. These are stock Bootstrap 5.3 utilities, themed via CSS variables — prefer them over bespoke CSS.

## Typography
- Headings: `h1`–`h6`; display: `display-1`…`display-6`.
- Helpers: `lead`, `small`, `text-muted`, `fw-light|normal|medium|semibold|bold`, `fst-italic`, `text-decoration-underline|line-through|none`, `text-truncate`.
- Alignment/transform: `text-start|center|end`, `text-uppercase|lowercase|capitalize`, `text-nowrap`, `text-break`.
- Inline elements: `mark`, `<del>`, `<s>`, `<ins>`, `<u>`, `<small>`, `<strong>`, `<em>`.
- Lists: `list-unstyled`, `list-inline` + `list-inline-item`.
- Blockquotes: `blockquote`, `blockquote-footer`.
- Page titles use `fs-*` + `fw-semibold` (see file 02 page-title row).

## Color & background
- Text: `text-{color}` (+ `text-{color}-emphasis` in BS 5.3).
- Background: `bg-{color}`, subtle `bg-{color}-subtle`, helper `text-bg-{color}` (bg + readable text in one).
- **Soft** tints (`bg-soft-{color}`) are a theme extension used for avatars/badges — confirm.
- Background opacity: `bg-opacity-10|25|50|75`. Text opacity: `text-opacity-25|50|75`.
- Element opacity: `opacity-0|25|50|75|100`.

> Always use the color tokens, not hex, so everything follows the active skin (see file 01/13).

## Borders & radius
- Borders: `border`, `border-{side}`, `border-{color}`, `border-0|1|…|5`.
- Radius: `rounded`, `rounded-{0..5}`, `rounded-circle`, `rounded-pill`, `rounded-{side}`.

## Shadows
`shadow-none`, `shadow-sm`, `shadow`, `shadow-lg`.

## Spacing
`m{t|b|s|e|x|y}-{0..5|auto}`, `p{…}-{0..5}`, `gap-{0..5}`, grid gutters `g-{0..5}` / `gx-`/`gy-`. Negative margins `m*-n{1..5}`.

## Flex & grid layout
- Flex: `d-flex`, `flex-row|column`, `justify-content-*`, `align-items-*`, `flex-wrap`, `flex-grow-1`, `flex-fill`, `order-*`, `gap-*`.
- Display: `d-{none|inline|block|flex|grid}` + responsive `d-{bp}-*`.
- Grid: `row` + `col`, `col-{1..12}`, `col-{bp}-*`, `row-cols-*`.

## Sizing
- Width: `w-25|50|75|100|auto`; height: `h-25|50|75|100|auto`; viewport: `vw-100`/`vh-100`, `min-vh-100`.
- Max: `mw-100`, `mh-100`.

## Position & misc
- Position: `position-{static|relative|absolute|fixed|sticky}`, edges `top-0`/`start-100`, `translate-middle{-x|-y}`.
- Object fit: `object-fit-{contain|cover|fill|scale|none}`.
- Overflow: `overflow-{auto|hidden|visible|scroll}`.
- Interaction: `pe-none|auto`, `user-select-{all|auto|none}`.
- Ratio (embeds): `ratio ratio-{1x1|4x3|16x9|21x9}`.
- Visibility: `visible`/`invisible`, screen-reader `visually-hidden`.

Next: [12 — Icons](12-icons.md).
