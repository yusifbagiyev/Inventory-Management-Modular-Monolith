# Inventory Pro — follow-up prompt (after the first redesign pass)

Paste this into Claude Code as the next message.

---

The redesign is in place and matches `design/`. Four consistency fixes remain, then a visual pass. Do not touch anything else.

1. **`Views/Products/Index.cshtml` — page actions.** The "Export PDF" button still uses `btn btn-outline-secondary`. Change it to `ip-btn ip-btn-secondary` with the label first and the icon last (`<span>@L["Export PDF"]</span><i class="fa-solid fa-file-pdf"></i>`), so it sits at the same height and radius as the primary "New Product" button next to it.

2. **`Views/Products/Index.cshtml` — inline inventory-code editing.** The save/cancel controls in `.inventory-code-edit` still use Bootstrap `btn btn-primary btn-sm` / `btn btn-secondary btn-sm` and `form-control form-control-sm`. Replace with: input → `ip-input mono` with `style="width:8ch;height:32px"` (or an `.ip-input-sm` class if you prefer — add it to `ip-components.css`), save → `ip-btn-icon` with `fa-solid fa-check`, cancel → `ip-btn-icon` with `fa-solid fa-xmark`. Keep the `save-inventory-code` / `cancel-inventory-code` / `inventory-code-input` hook classes and `data-id` attributes unchanged; the JS depends on them. Wrap the three in a `d-flex align-items-center gap-1` div instead of `input-group`.

3. **`Views/Shared/_Layout.cshtml` — global modals.** `#globalImageModal` and `#globalConfirmModal` have no design classes. Add `ip-modal` to both `.modal` elements. In the confirm modal, change the footer buttons to `ip-btn ip-btn-secondary` (Cancel) and `ip-btn ip-btn-primary` (Confirm); when `confirmAction()` is called for a destructive action (delete, reject), swap the primary for `ip-btn ip-btn-danger` — add an optional `{ danger: true }` argument to `confirmAction()` in `site.js` if it does not exist, and use it at the existing delete call sites.

4. **`wwwroot/css/modern-ui.css` — dead rules.** The file predates `ip-components.css` and still carries overrides for classes the views no longer render. Grep the views and JS for each selector in these blocks and delete the ones with zero usages: `.card.bg-*` (~lines 568–573), `.nav-tabs .nav-link .badge` (~418), the `.badge.bg-*` recolouring (~294–302) if no view still emits `badge bg-*`, and any `.modal-header[class*="bg-"]` rule. Do not remove the `:root, [data-bs-theme]` alias block at the top or anything a view still references. Aim to shorten the file, not to rewrite it.

Then run the app in both themes and open each of these pages once, fixing only what is visibly broken (overlap, unstyled control, wrong colour): Login, Dashboard, Products, Product Details, Product Edit, Transfer, Routes, Approvals (open the details modal, try Reject), Notifications. Report what you changed per page in one line each.

Commit as two commits: "ui: align remaining Bootstrap controls with ip-components" (steps 1–3) and "css: drop unused modern-ui overrides" (step 4 + visual fixes).
