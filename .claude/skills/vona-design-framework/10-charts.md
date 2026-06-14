# 10 — Charts (Chart.js)

From `charts`. Vona uses **Chart.js** (canvas-based). Each chart is a `<canvas>` initialized by a small JS block.

## Container + init pattern
```html
<div class="card">
  <div class="card-header"><h5 class="card-title mb-0">Revenue</h5></div>
  <div class="card-body">
    <div style="height:320px">
      <canvas id="revenueChart"></canvas>
    </div>
  </div>
</div>
```
```js
new Chart(document.getElementById('revenueChart'), {
  type: 'line',                 // line | bar | pie | doughnut | radar | polarArea | bubble | scatter
  data: { labels: [...], datasets: [{ label: '...', data: [...] }] },
  options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'top' } } }
});
```
- Wrap the canvas in a **fixed-height** element and set `maintainAspectRatio:false` so it fills the card cleanly.
- Load `chart.js` (page `@section`/script), not globally.

## Chart types demonstrated
| Category | Variants on the demo |
|---|---|
| Line | basic, interpolation, multi-axis, point styling, segment, stepped |
| Area | basic, different dataset, stacked, boundaries, draw-time |
| Bar | basic, border-radius, floating, horizontal, stacked, stacked-groups, vertical |
| Pie / Doughnut | pie, multi-series pie, doughnut |
| Polar Area | polar area |
| Radar | radar |
| Bubble / Scatter | bubble, scatter |
| Combo | bar + line, stacked bar + line |

(Area = a `line` chart with `fill:true`.)

## Theme-aware colors
Charts must follow the active skin + light/dark mode:
- Read CSS variables at init rather than hardcoding hex:
```js
const css = getComputedStyle(document.documentElement);
const primary = css.getPropertyValue('--bs-primary').trim();
const gridColor = css.getPropertyValue('--bs-border-color').trim();
```
- Use `--bs-primary`, `--bs-success`, etc. for series; `--bs-border-color` for grid lines; `--bs-secondary-color`/`--bs-body-color` for ticks/labels.
- **On theme/skin change** (file 13), re-read variables and call `chart.update()` (or destroy + recreate) so colors track the new theme.

> If your licensed build actually ships a different chart lib on some pages (e.g. ApexCharts for the dashboard widgets), follow that lib's API for those pages — but the `charts` demo page is Chart.js.

Next: [11 — Typography & utilities](11-typography-and-utilities.md).
