# Product screenshots

Drop real product screenshots here using the **exact filenames** below. The marketing
pages reference these paths; when a file is present it replaces the placeholder block
automatically (see `Views/Shared/Marketing/_Screenshot.cshtml`). No code change needed.

| Filename | Used on | Suggested size | Shows |
|---|---|---|---|
| `admin-dashboard.png`    | Home (landing)      | 1600×900 (16:9) | Tenant admin console dashboard / overview |
| `admin-applications.png` | Features → Admin    | 1600×900 (16:9) | Applications list (OAuth clients) |
| `portal-security.png`    | Features → Portal   | 1600×900 (16:9) | End-user portal Security/MFA page |

## Tips
- Capture at a 16:9 ratio (e.g. 1600×900 or 1920×1080) so the layout doesn't shift.
- Export as PNG (or WebP). Use both light and dark UI as you prefer — one image per slot.
- Keep file sizes reasonable (< ~400 KB); compress before committing.
- To add more placeholders, render the partial in a view:
  `@await Html.PartialAsync("Marketing/_Screenshot", new Authly.Web.Models.ScreenshotVM("Caption", "your-file.png"))`
