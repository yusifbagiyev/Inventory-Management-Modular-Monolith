# Inventory Pro — prompt 3: responsive (tablet + phone)

Paste into Claude Code. Reference design: `design/screens/10-mobile.jpg` (and the live file `IP 10 Mobile.dc.html` if available).

---

Make the redesigned UI work on tablet (768–1024px) and phone (≤767px). `design/ip-components.css` has been updated with a responsive layer at the bottom (search for "Responsive layer") — copy the new file over `wwwroot/css/ip-components.css` **unchanged** first. Then do the markup and JS work below. Do not add new colours, fonts, or a second CSS file.

## 1. Phone app bar + drawer (`_Layout.cshtml`, `_Sidebar.cshtml`, `site.js`)

- Add directly inside `.ip-app`, before `.ip-rail`:
  ```html
  <header class="ip-appbar">
    <button class="ip-btn ip-btn-icon" type="button" data-rail-open aria-label="@L["Menu"]"><i class="fa-solid fa-bars"></i></button>
    <div class="title">@ViewData["Title"]</div>
    <a class="ip-btn ip-btn-icon position-relative" asp-controller="Notifications" asp-action="Index" aria-label="@L["Notifications"]"><i class="fa-regular fa-bell"></i><span class="ip-dot" data-unread-dot></span></a>
  </header>
  <div class="ip-rail-backdrop" data-rail-close></div>
  ```
  The dot is shown/hidden by the existing unread-count poller (reuse the same element toggling you already do for the desktop bell).
- In `_Sidebar.cshtml`: add `<button class="ip-rail-close" type="button" data-rail-close><i class="fa-solid fa-xmark"></i></button>` at the end of `.ip-rail-brand`. In `.ip-rail-foot`, above the user row, add a row with the language segment and theme button (same markup as the desktop toolbar; `ip-seg` + `ip-btn-icon lg`). They're hidden on desktop by CSS and shown in the drawer on phone.
- `site.js` `Rail` module: on `[data-rail-open]` click add `.open` to `.ip-rail`, `.show` to `.ip-rail-backdrop`, `ip-drawer-open` to `body`; `[data-rail-close]`, backdrop click, and `Escape` reverse it. Close the drawer after a nav link click. On `matchMedia('(min-width:768px)')` change, remove all three classes. Do not persist the open state. Keep the existing collapse logic for ≥768px untouched.
- Pages that are "detail" pages (Product Details, Route Details, Approval detail, Edit/Create, Transfer step 2) set `ViewData["BackUrl"]`; when it is set, the app bar renders a back arrow (`fa-arrow-left`, `href=BackUrl`) instead of the hamburger, and the title gets a `<small>` with the inventory code where there is one.

## 2. Tables → row cards (`Products/Index`, `Routes/Index`, `Approvals/Index`, `MyRequests/Index`, `UserManagement/Index`, `Categories/Index`, `Departments/Index`)

The CSS turns `.ip-table` into stacked cards below 768px using class hooks on `<td>`. Add these classes (no other markup change):

- `td.thumb` — image/icon cell (first column).
- `td.code` — inventory code (goes top-right).
- `td.title` — primary text cell; keep `.primary` on the name and `.sub` under it.
- `td.meta` — department + worker cell; `.sub` becomes inline with " · ".
- `td.state` — status badge + pending text.
- `td.date` — date (goes bottom-right).
- `td.actions` — row action buttons (hidden on phone; the row is clickable → details).
- `td.hide-sm` — any column that is noise on a phone (e.g. "Created by").
- Any remaining plain cell: add `data-label="@L["Column"]"` and the CSS prints the label above the value.

Make the whole row navigate on phone: rows already have `data-href`; ensure `site.js` only intercepts clicks that are not on `a`/`button` inside the row.

Approvals / MyRequests have no thumbnail: use `td.title` for the product, `td.meta` for type + requester, `td.date`. The detail opens in the same Bootstrap modal — CSS turns `.ip-modal` into a bottom sheet on phone; add `td.field`, `td.old`, `td.arrow`, `td.new` to the `.ip-diff` table cells.

## 3. Filters → "Filtr" sheet (`Products/Index`, `Routes/Index`)

- After the search field in `.ip-filterbar`, add:
  ```html
  <button class="ip-filter-more" type="button" data-bs-toggle="modal" data-bs-target="#filterSheet"><i class="fa-solid fa-sliders"></i>@L["Filter"]<span class="count">@activeFilterCount</span></button>
  ```
  where `activeFilterCount` = number of non-default select values (server-side; empty string when 0 so the badge hides).
- Add `#filterSheet` (`modal fade ip-modal`): title "Filtrlər" + ghost "Sıfırla" link to the page without query; body: one `.ip-chips` group per filter with `.ip-chip` buttons (`data-filter="category" data-value="…"`, `.active` for current), footer: `ip-btn ip-btn-secondary` "Bağla" + `ip-btn ip-btn-primary` "N məhsulu göstər" (`data-filter-apply`). JS: clicking a chip toggles `.active` within its group (single select), "göstər" builds the query string from active chips and the current search box, then navigates. On tablet the sheet is the home for filters from the 3rd onward (the CSS hides them in the bar); on desktop the button is hidden by CSS.

## 4. Forms and sticky footers (`Products/Create|Edit`, `Routes/Transfer`, `Routes/Edit`, `UserManagement/*`, `Categories/*`, `Departments/*`)

- Every form already ends with `.ip-form-foot`; make sure its buttons are in this order: `[danger-ghost or ghost]` `[secondary Cancel]` `[primary Submit]`, and wrap icon-only text in `<span>` so CSS can hide the label on phone (`<button class="ip-btn ip-btn-danger-ghost"><i class="fa-solid fa-trash"></i><span>@L["Delete"]</span></button>`).
- Transfer step 1: add a QR/camera button next to the code input (`ip-btn ip-btn-secondary ip-btn-icon`, `fa-qrcode`) that opens `<input type="file" accept="image/*" capture="environment">` — leave decoding as a `// TODO: QR decode` for now, it is UI only.
- Product Details: wrap the gallery in `.ip-gallery` (exists) and append `<div class="ip-gallery-dots">` with one `<i>` per image (`.active` on the current). Add touch swipe in `site.js` (pointerdown/up, 40px threshold) that calls the existing prev/next. The action row (Edit / Transfer / Delete) moves into `.ip-form-foot` on the details page too, so it becomes the fixed bottom bar on phone.

## 5. Dashboard (`Home/Dashboard`)

- KPIs: 2×2 grid on phone (CSS does it). Shorten labels through resources only where needed: `Dashboard.Kpi.Active = "Aktiv"`.
- Chart.js: set `maintainAspectRatio:false` and give the bar chart container `style="height:200px"` desktop / `160px` phone via a `.ip-chart` class + media query (add `.ip-chart{height:220px} @media(max-width:767px){.ip-chart{height:160px}}` to `ip-components.css`). Labels on phone: use `"1–5"` style day ranges instead of `"01–05.09"` (`ticks.callback` checks `window.innerWidth < 768`).
- Order on phone: KPIs → "Diqqət tələb edir" → chart → categories → departments. Use CSS `order` on the existing sections inside a flex column at ≤767px; do not duplicate markup.

## 6. Checks

Run at 390×844, 768×1024, 1366×768 in both themes:
- Drawer opens/closes, closes on nav, body does not scroll behind it.
- Products list: row cards, code top-right, date bottom-right, status row, tap → details, fixed footer with pagination + "Yeni məhsul".
- Filter sheet applies and resets; count badge correct.
- Approval detail opens as bottom sheet; Approve/Reject work; reject reason field visible above the keyboard.
- Edit/Create/Transfer: fixed footer never covers the last field (there is 96px bottom padding on `.ip-main`).
- No horizontal scroll on phone except inside `.ip-table-wrap` on tablet.
- All tap targets ≥44px.

Commit as "ui: responsive layer — app bar, drawer, row cards, bottom sheets". Report one line per page.
