# Charts (Chart.js, themed)

The project standardizes on **Chart.js** (CDN). Apex/Flot/Morris-style pages are all rendered with
Chart.js using brand tokens.

## Setup
Add to `<head>` (chart pages only):
```html
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
```
Theme defaults once, after `mountShell`:
```js
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const rgb = (v, a = 1) => `rgb(${css(v)} / ${a})`;
Chart.defaults.font.family = "Inter";
Chart.defaults.color = rgb("--muted-foreground");
Chart.defaults.borderColor = rgb("--border");
```
Wrap each canvas in a fixed-height box: `<div class="h-64"><canvas id="c1"></canvas></div>`, and set
`options.maintainAspectRatio = false`.

## Palette
Use `rgb('--primary')`, `rgb('--teal')`, `rgb('--mint')`, `rgb('--coral')`, `rgb('--warning')`,
`rgb('--success')`. Fills: `rgb('--primary', .12)`.

## Recipes
**Area / line**
```js
new Chart(c1,{type:"line",data:{labels,datasets:[{data,borderColor:rgb("--primary"),
  backgroundColor:rgb("--primary",.12),fill:true,tension:.4,borderWidth:2,pointRadius:0}]},
  options:{maintainAspectRatio:false,plugins:{legend:{display:false}},
  scales:{x:{grid:{display:false}},y:{border:{display:false},ticks:{maxTicksLimit:5}}}}});
```
**Bar**
```js
datasets:[{data,backgroundColor:rgb("--primary"),borderRadius:6,barThickness:14}]
// stacked: scales:{x:{stacked:true},y:{stacked:true}}
```
**Doughnut**
```js
new Chart(c,{type:"doughnut",data:{labels,datasets:[{data,
  backgroundColor:[rgb("--primary"),rgb("--teal"),rgb("--mint"),rgb("--coral")],borderWidth:0}]},
  options:{maintainAspectRatio:false,cutout:"68%"}});
```
**Radial gauge:** doughnut with `rotation:-90, circumference:180, cutout:"75%"`.

## Funnel (no chart lib — use `.progress`)
```html
<div><div class="flex justify-between text-sm mb-1"><span>Leads</span><span class="text-muted-foreground">71%</span></div>
  <div class="progress"><span class="bg-teal" style="width:71%"></span></div></div>
```

## Legend
Prefer custom HTML legends below the chart (rows of colored dot + label + value) for tight control;
otherwise `plugins.legend.labels = {boxWidth:8, usePointStyle:true}`.
