# Inventory Pro — redesign handoff (Claude Code)

Target: `InventoryManagement.Web` (ASP.NET MVC, Razor, Bootstrap 5.3, Font Awesome 7, Chart.js 4). No new JS framework, and nothing loaded from a CDN at runtime.

Reference designs (open in browser): `IP 00 Design System.dc.html`, `IP 01 … IP 09 *.dc.html`. The design files use inline styles because the design tool requires it. **Do not copy the inline styles.** Implement them as the `.ip-*` classes below, built on the tokens.

## 0. Rules (read first)
- Neutral gray base with one accent (`--ip-accent`, a deep blue-teal). Use the accent only for: the primary button, focus ring, active tab underline, unread dot, links.
- Status colors (success / warning / danger / info) are for status only: working/broken, completed/pending, approved/rejected.
- "Yeni", type, category, and role are **plain text** (`--ip-text-3`), never colored badges.
- Secondary status (e.g. "Təsdiq gözləyir" under the main badge) is colored text only, with no background.
- Brand yellow `#FFC000` goes in exactly two places: the logo mark and the cover-image star.
- Structure: a 2px rule separates major sections (`--ip-rule` strong, `--ip-rule-soft` light); a 1px rule (`--ip-border`) separates table rows. No cards on content. Shadows only on floating UI (dropdown, modal, toast).
- Everything is flush-left. Wide buttons: label on the left, icon pushed to the right edge (`justify-content: space-between`).
- Radius: 10px controls, 12px panels/images, 16px modal, 6px badges.
- Density: 44px thumbnails, 12px cell padding, 40px controls.
- Dates `dd.MM.yyyy`, times `HH:mm`. Numbers use `font-variant-numeric: tabular-nums`. Inventory codes use `--ip-font-mono`.

## 1. Tokens
1. Copy `inventory/tokens.css` → `wwwroot/css/tokens.css`. Load it **after** `bootstrap.min.css` and **before** `site.css` / `modern-ui.css`.
2. Remove every hard-coded `#0E9BC4` / `bg-info` / `bg-primary` accent from `modern-ui.css` and `site.css`, and switch to `var(--ip-*)`.
3. Dark mode: `data-bs-theme="light|dark"` on `<html>`. Put this inline in `<head>` of `_Layout.cshtml` before any CSS, so there is no flash on load:
   ```html
   <script>document.documentElement.setAttribute('data-bs-theme', localStorage.getItem('ip-theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
   ```
   Toggle button: flip the attribute, save to `localStorage('ip-theme')`, then call `window.dispatchEvent(new Event('ip-theme'))` so charts re-render.
4. Fonts: system stack only (`--ip-font-sans`, `--ip-font-mono`). Delete any Google Fonts `<link>`.
5. Icons: serve Font Awesome 7 from `wwwroot/lib/fontawesome/`, not a CDN. The class names in the designs (`fa-solid fa-box`, `fa-regular fa-bell` …) are the same in FA7.

## 2. Component classes → `wwwroot/css/ip-components.css`
`design/ip-components.css` is already written — copy it as-is. The table below is the summary of what it contains:

| Class | Spec |
|---|---|
| `.ip-btn` + `-primary / -secondary / -ghost / -danger`, `-icon` | h 40 (icon 34), r 10, weight 600 (secondary/ghost 500). Hover: `--ip-accent-hover` / `--ip-surface-hover`. `:focus-visible` 2px accent outline, offset 2. `:disabled` opacity .45. Danger = `--ip-danger-solid` + white. |
| `.ip-field`, `.ip-input`, `.ip-select`, `.ip-textarea` | h 40, r 10, 1px `--ip-border-strong`. Focus: border accent + `0 0 0 3px var(--ip-focus-ring)`. `.is-invalid`: border `--ip-danger-text` + 3px `--ip-danger-soft` ring + message below with `fa-circle-exclamation`. Disabled: `--ip-sunken` bg, `--ip-text-3`. |
| `.ip-filter` | filter-bar control: `--ip-sunken` bg, no border, label inside ("Şöbə  Hamısı ▾"). |
| `.ip-badge` + `-success / -warning / -danger / -info / -neutral` | h 24, r 6, 12px/500, soft bg + text color, 6px `currentColor` dot. |
| `.ip-table` | `th` 12px/500 `--ip-text-3`, no fill. `td` 12px vertical padding, `border-top: 1px var(--ip-border)`. Row hover `--ip-surface-hover`. Last row followed by the 2px pagination rule. |
| `.ip-tabs` | 44px, active = 600 + 2px underline (accent on Approvals/Notifications, `--ip-rule` on Dashboard/Routes). Count as plain 12px text; the pending count is `--ip-warning-text`. |
| `.ip-switch` | 40×24 track, r 12, accent when on. Use `<input type="checkbox" role="switch" class="form-check-input">` restyled. |
| `.ip-toast` | Bootstrap toast restyled: surface, 1px border, r 12, `--ip-shadow-lg`, bottom-right, 4 s. Icon `fa-circle-check` success / `fa-circle-xmark` danger. |
| `.ip-modal` | Bootstrap modal: r 16, header with bottom 2px `--ip-rule`, footer buttons right-aligned. Backdrop `--ip-backdrop`. |
| `.ip-empty` | flush-left: 48px sunken icon tile, 18px/600 title, one sentence, one secondary button. |
| `.ip-skeleton` | `--ip-surface-active` blocks, `ipPulse` 1.3 s; add `aria-busy="true"` on the container. |
| `.ip-thumb` | 44×44 (list), r 10, `--ip-sunken` bg, `object-fit: cover`. Category icon as fallback when there is no image. |

## 3. Screen by screen (design file → Razor view)

| Design | View(s) | Notes |
|---|---|---|
| IP Rail | `Views/Shared/_Sidebar.cshtml` | Always dark: `<aside data-bs-theme="dark">`, bg `--ip-sunken`. Groups: –, Uçot, Nəzarət, Kataloq. Active = `--ip-surface-active` + 600. Counts: product count as plain text; Approvals/Notifications count as accent-soft pill. Collapse to 72px (icons only, 7px accent dot on items with alerts), stored in `localStorage('ip-rail')`. User + logout at the bottom. Remove `bg-primary / bg-warning / bg-danger / bg-info` badges. |
| IP Toolbar | `_Layout.cshtml` header | No bar background, sits inside the content area. Left to right: code search (`Kod ilə tap`), AZ/EN segmented (posts to `LanguageController`), theme toggle, bell (8px accent dot, dropdown of the latest 4, "Hamısını oxunmuş et", "Bütün bildirişlər"). |
| 01 Login | `Account/Login.cshtml` | Split: left dark panel (logo, 2px rule, one sentence), right form max-width 400. Server error → danger-soft alert above the fields. Password show/hide button. |
| 02 Dashboard | `Home/Dashboard.cshtml` | Period tabs (7/30/90/Hamısı) → existing period query. KPI = 2px top rule, 40px number, trend = `fa-arrow-trend-up/down` + delta colored by *meaning* (fewer pending = success), use `Previous*` from `DashboardViewModel`; hide the trend for "Hamısı". Charts: see §4. "Diqqət tələb edir" list: pending approvals, `PendingTransfers`, broken count. |
| 03 Products | `Products/Index.cshtml` | Filter bar (search, Kateqoriya, Şöbə, Vəziyyət, Sıfırla only when a filter is active) between 2px rule and 1px rule. Columns: thumb, Kod (mono), Məhsul (model 600 + "Yeni" text; vendor · category below), Şöbə və işçi (2 lines), Yenilənib, Vəziyyət (`IsWorking` badge; `HasPendingApproval` → warning text line), actions (eye/pen/transfer icon buttons). Whole row is clickable → Details. Skeleton while loading via `_Skeleton.cshtml`; empty state when there are 0 results. |
| 04 Details | `Products/Details.cshtml` + `_ImageGallery.cshtml` | Pending-approval banner (warning-soft, link to the request). Gallery 4:3 with prev/next, counter, "Əsas şəkil" chip, thumbnails with 2px accent on the active one. Info as `<dl>` rows (160px label column). Route history table below. |
| 05 Edit/Create | `Products/Create.cshtml`, `Edit.cshtml` + `_ImageManager.cshtml` | Sections split by rules: Əsas məlumat / Yerləşmə / Vəziyyət (switches for `IsWorking`, `IsActive`, `IsNewItem`) / Təsvir (counter /500) / Şəkillər. Image tiles: star (top-left, sets `CoverImageUrl`; the cover gets a 2px accent border and a yellow star), × (top-right, adds to `RemoveImageUrls`), dashed "Şəkil əlavə et" tile (`ImageFiles`). Sticky footer with a 2px top rule: operator note, Ləğv et, Yadda saxla. Duplicate `InventoryCode` → inline error "Bu kod artıq istifadə olunur: {Model}". |
| 06 Transfer | `Routes/Transfer.cshtml` | Step 1: large mono code input + Axtar (Enter works) → found-product strip (sunken) or inline "tapılmadı" error. Step 2 only when found: Hədəf şöbə (error if same as current), Yeni işçi, Qeyd, optional photos. Bottom bar: "Maliyyə → İT · worker" summary + primary "Transferi başlat". |
| 07 Routes | `Routes/Index.cshtml` | Tabs Hamısı / Gözləyir / Tamamlanıb with counts, filter bar, table: date+time, product (thumb+model+code), from → to (dept + worker), type text, status badge, "Tamamla" secondary for pending rows. |
| 08 Approvals | `Approvals/Index.cshtml` + `_ApprovalDetails.cshtml` | Header stats as plain numbers (pending in warning text). Tabs by `ApprovalStatus`. Row: #id mono, "Type: object" + changed field names, requester, date, badge, "Bax və qərar ver". Modal diff table from `ActionData`: Sahə / Hazırkı (text-2) / → / Təklif olunan (accent-soft highlight, 600). Reject needs a reason (textarea appears inline; empty → error). Esc and backdrop click close the modal. Toast after the decision. |
| 09 Notifications | `Notifications/Index.cshtml` | Tabs Hamısı / Oxunmamış, type select, "Hamısını oxunmuş et". Grouped by day (Bu gün / Dünən / dd.MM.yyyy). Item: 8px accent dot + 600 title when unread; 32px neutral type-icon tile (**no colored circles**); click marks as read; optional action link. No left border stripe. |

## 4. Chart.js 4 (dashboard)
Read the colors from the tokens so the theme switch works:
```js
const v = n => getComputedStyle(document.documentElement).getPropertyValue(n).trim();
Chart.defaults.font.family = v('--ip-font-sans');
Chart.defaults.color = v('--ip-text-3');
// Transfer activity — stacked bar
{ type:'bar', data:{ labels, datasets:[
    { label:'Tamamlanıb', data:completed, backgroundColor:v('--ip-chart-1'), borderRadius:4, maxBarThickness:64 },
    { label:'Gözləyir',   data:pending,   backgroundColor:v('--ip-chart-pending'), borderRadius:4, maxBarThickness:64 } ]},
  options:{ plugins:{ legend:{ display:false } },
    scales:{ x:{ stacked:true, grid:{ display:false }, border:{ color:v('--ip-border-strong') } },
             y:{ stacked:true, grid:{ color:v('--ip-border') }, border:{ display:false }, ticks:{ precision:0 } } } } }
// Categories — doughnut
{ type:'doughnut', data:{ labels, datasets:[{ data, backgroundColor:[1,2,3,4,5].map(i=>v('--ip-chart-'+i)), borderColor:v('--ip-surface'), borderWidth:2 }] },
  options:{ cutout:'72%', plugins:{ legend:{ display:false } } } }
```
Ignore `CategoryDistribution.Color` from the server, or change it to return the index. Build the legend as HTML (swatch, name, %, count). Re-create both charts on the `ip-theme` event. Bar click → Routes list filtered by `BucketStarts[i]`/`BucketEnds[i]`.

## 5. Copy / i18n
- Keep all strings in `.resx` (`@L["…"]`). AZ strings are longer than EN, so never set a fixed width on buttons or tabs; use `min-width` + `white-space: nowrap` only on badges.
- New keys used by the designs: Diqqət tələb edir, Tamamlanmamış transferlər, Nasaz cihazlar, Təsdiq gözləyir, Əsas şəkil, Əsas şəkil et, Şəkil əlavə et, Heç nə tapılmadı, Filtrləri sıfırla, Transferi başlat, Bax və qərar ver, Rədd etmə səbəbi, Rədd etməni təsdiqlə, Hamısını oxunmuş et, Bütün bildirişlər, Kod ilə tap, Menyunu yığ.

## 6. Order of work (one commit each)
1. tokens.css + theme script + remove the old accent colors.
2. `_Sidebar` + header (rail, toolbar, bell dropdown).
3. Component classes (§2) + Bootstrap overrides.
4. Products Index → Details → Create/Edit + `_ImageManager`.
5. Transfer, Routes.
6. Approvals + modal, Notifications.
7. Dashboard + charts.
8. Login.

## 7. Check before merging
- WCAG AA: text ≥ 4.5:1 in both themes (values listed in tokens.css comments). Test with the browser's contrast checker.
- Keyboard: every action reachable by Tab; visible 2px accent focus; Esc closes the modal and dropdowns.
- 1366px: product table fits without horizontal scroll with the rail expanded. Below 1024px the rail starts collapsed. Below 768px tables scroll horizontally inside their wrapper.
- No request to an external host in the Network tab.
