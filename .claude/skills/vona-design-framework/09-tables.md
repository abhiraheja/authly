# 09 — Tables

Two tiers: **static** Bootstrap tables (`tables-static`) and **DataTables**-powered interactive tables (`tables-datatables-*`).

## Static tables (Bootstrap)
```html
<div class="table-responsive">
  <table class="table table-striped table-hover align-middle mb-0">
    <thead class="table-light">
      <tr><th>#</th><th>Name</th><th>Status</th></tr>
    </thead>
    <tbody>
      <tr><td>1</td><td>Jane</td><td><span class="badge badge-lighten-success">Active</span></td></tr>
    </tbody>
  </table>
</div>
```

Variant classes:
| Variant | Class |
|---|---|
| Base | `table` |
| Theme custom | `table-custom` *(extension — confirm)* |
| Striped rows / columns | `table-striped` / `table-striped-columns` |
| Hover | `table-hover` |
| Active row/cell | `table-active` |
| Bordered / borderless | `table-bordered` / `table-borderless` |
| Small (dense) | `table-sm` |
| Group divider | `table-group-divider` |
| Head light / dark | `table-light` / `table-dark` on `<thead>` |
| Contextual rows | `table-{color}` on `<tr>`/`<td>` |
| Responsive wrapper | `.table-responsive` (or `-sm|md|lg|xl`) |
| Caption | `<caption>` |

Common cell content: avatars (file 04), badges (file 03), action dropdowns/icon-buttons, progress bars.

## DataTables (interactive)
The `tables-datatables-*` pages each demo a feature. A DataTable is a normal `<table>` upgraded by the DataTables library.

```html
<table id="usersTable" class="table table-hover w-100">
  <thead><tr><th>Name</th><th>Email</th><th>Role</th></tr></thead>
  <tbody><!-- rows --></tbody>
</table>
```
```js
new DataTable('#usersTable', {
  responsive: true,
  pageLength: 10,
  // buttons / select / ajax / columns config per feature
});
```

Feature pages (load only what the page needs):
| Page | Feature | Extra libs |
|---|---|---|
| `tables-datatables-basic` | paging/search/sort | DataTables core |
| `tables-datatables-export-data` | copy/CSV/Excel/PDF/print | Buttons + JSZip + pdfmake |
| `tables-datatables-select` | row selection | Select extension |
| `tables-datatables-checkbox-select` | checkbox column select | Select + checkbox col |
| `tables-datatables-ajax` | server/ajax data source | `ajax` option |
| `tables-datatables-javascript` | data from JS array | `data` option |
| `tables-datatables-rendering` | custom cell render | `columns.render` |
| `tables-datatables-columns` | column control/visibility | ColVis/Buttons |
| `tables-datatables-child-rows` | expandable detail rows | child-row API |

Notes:
- Style DataTables to match Bootstrap 5 (use the DataTables-BS5 integration the package ships).
- Re-render Lucide icons inside cells after a DataTables draw (`drawCallback`).
- Keep `w-100` on the table so layout doesn't jump on init.

Next: [10 — Charts](10-charts.md).
