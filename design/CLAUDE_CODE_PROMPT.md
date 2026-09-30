# Inventory Pro — Claude Code üçün prompt

Aşağıdakı mətni Claude Code-a olduğu kimi göndərin. Yanında bu qovluğu (`inventory/`) reponun `design/` qovluğuna kopyalayın.

---

You are redesigning the UI of `InventoryManagement.Web` (ASP.NET MVC, Razor, Bootstrap 5.3, Font Awesome 7, Chart.js 4, vanilla JS only). Everything you need is in `design/`:

- `design/tokens.css` — final CSS custom properties for light and dark themes, already mapped to Bootstrap 5.3 variables. Copy it to `wwwroot/css/tokens.css` **unchanged**.
- `design/ip-components.css` — the component layer (`.ip-*` classes). Copy it to `wwwroot/css/ip-components.css`. Extend it only if a screen needs something that is missing; never inline styles in Razor.
- `design/CLAUDE_CODE_HANDOFF.md` — the rules, screen-by-screen mapping to Razor views, Chart.js config, new resource keys, and the order of work. Read it fully before touching code.
- `design/screens/*.png` — reference screenshots of every screen (light + dark).

Goal: replace the current bright-blue, badge-heavy UI with a calm neutral system: gray base, one blue-teal accent, status colors only where they carry meaning, 2px rules instead of cards, flush-left labels, dark mode with `data-bs-theme`, no external fonts or CDNs.

Work in the order given in section 6 of the handoff, one commit per step, and after each step run the app and confirm:
1. no reference to `#0E9BC4`, `bg-primary`, `bg-info`, `bg-warning`, `bg-danger` remains in views or CSS for non-status purposes;
2. both themes render without unstyled flashes (theme script is inline in `<head>` before CSS);
3. no request to an external host in the Network tab;
4. Azerbaijani strings of any length fit (no fixed widths on buttons, tabs, or table cells).

Keep all user-facing text in the existing `.resx` files via `@L["…"]`; add the new keys listed in section 5 of the handoff. Do not change controllers, DTOs, or routes unless the handoff explicitly asks for it (Chart color index, `CommentedByMe`-style fields are out of scope). When a design detail is ambiguous, follow `ip-components.css` and the screenshot, in that order, and leave a `// DESIGN?` comment rather than inventing a new pattern.

Start with step 1 (tokens + theme script + removing the old accent) and show me the diff before continuing.
