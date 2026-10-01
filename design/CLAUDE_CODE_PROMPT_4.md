# Inventory Pro — prompt 4: remove template-looking patterns

Paste into Claude Code after prompt 3. Copy `design/ip-components.css` over `wwwroot/css/ip-components.css` first (login block, h1 size and notification icon rule changed).

---

A review flagged several screens as looking like a generic template. Apply these changes exactly; do not restyle anything else.

1. **Login (`Account/Login.cshtml`)** — drop the two-column layout with the dark side panel and the tagline. New structure:
   ```html
   <div class="ip-login">
     <header class="ip-login-head">
       <div class="d-flex align-items-center gap-2"><span class="ip-logo"><i class="fa-solid fa-box-archive"></i></span><strong>Inventory Pro</strong></div>
       <div class="d-flex align-items-center gap-2">[language ip-seg] [theme ip-btn-icon]</div>
     </header>
     <main class="ip-login-main">
       <form class="ip-login-form" method="post">
         <h1>@L["Login.Title"]</h1>
         [validation summary as .ip-banner.ip-banner-danger, only when there is an error — text: "İstifadəçi adı və ya şifrə yanlışdır."]
         [ip-field İstifadəçi adı, autofocus]
         [ip-field Şifrə with the existing show/hide button]
         <div class="row"><label class="d-flex align-items-center gap-2 ip-muted"><input type="checkbox" class="ip-check" checked> @L["RememberMe"]</label><button class="ip-btn ip-btn-primary" style="min-width:0">@L["Login.Submit"]</button></div>
       </form>
     </main>
     <footer class="ip-login-foot"><span>@L["Login.Help"] <a href="mailto:…">it@sirket.az</a></span><span>v@version</span></footer>
   </div>
   ```
   Delete the resource keys for the tagline/claim and "© year". Add `Login.Help = "Daxili sistem. Giriş problemi üçün İT şöbəsi:"`. The submit button has no trailing icon.

2. **Page titles** — `h1` is now 24px (CSS). Remove the subtitle line under the title on Transfer ("Məhsulu inventar kodu ilə tapın…") and anywhere else the subtitle merely restates the page name. Keep subtitles that carry data ("248 məhsul · 231 aktiv · 2 nasaz", "6 oxunmamış").

3. **Dashboard KPIs (`Home/Dashboard`)** — remove the weekday/date line above the title and remove the trend arrows and "+N əvvəlki N günə nisbətən" text from all four tiles. New tiles (label / value / one-line secondary):
   - Ümumi məhsul / total / "{active} aktiv · {inactive} deaktiv"
   - Nasaz / count / names of up to two faulty products, comma-separated (link to Products filtered by Nasaz)
   - Tamamlanmış transfer / count / "son N gündə" (from the period selector; "bütün dövrdə" for All)
   - Gözləyən transfer / count / "ən köhnəsi N gündür" (age of the oldest pending route)
   Remove `PreviousPeriod*` fields from `DashboardViewModel` if nothing else uses them.

4. **Transfer (`Routes/Transfer`)** — remove the step numbers "1" / "2" in front of section headings; rename "Məhsulu tapın" → "Məhsul". The found-product card loses its filled rounded background: render it as a row between two 1px rules (`border-top/bottom: 1px solid var(--ip-border)`, padding 14px 0, 44px thumb) — add `.ip-found-row` to `ip-components.css` for this.

5. **Product Details (`Products/Details`)** — remove "Sil" from the header action row and the "Aktiv" word next to the status badge. Add a text button `Məhsulu sil` (13px, danger colour, underlined, no icon) under the information list, which opens the existing confirm modal with `danger: true`. The primary "Transfer et" button has no trailing arrow icon.

6. **Notifications (`Notifications/Index` and the dropdown)** — remove the icon tile (`.ip-notif-icon`) from each row; the row is: unread dot · title + text + optional link · time. Row padding 12px.

7. **Everywhere** — remove trailing `fa-arrow-right` icons from primary buttons except where the action is literally "go to the next step" (Transfer step → submit keeps none either). Primary buttons keep their label flush-left and the right-side icon slot empty, so drop `justify-content: space-between` where the icon is gone.

Run both themes on Login, Dashboard, Transfer, Details, Notifications. Commit as "ui: remove template-looking patterns (login, kpis, titles, notifications)".
