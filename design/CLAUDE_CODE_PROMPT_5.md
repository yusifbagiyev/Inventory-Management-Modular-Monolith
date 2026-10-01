# Inventory Pro — prompt 5: login as account picker (design 2a)

Paste into Claude Code. Replaces section 1 of prompt 4. Copy `design/ip-components.css` over `wwwroot/css/ip-components.css` first (the login block was rewritten). Reference: `IP 01 Login - Variants.dc.html`, option 2a.

---

Rebuild the login page as a two-step flow for a small team: pick an account, then type only the password. Stay inside `AccountController` + `Views/Account/Login.cshtml`; no new JS framework, no external requests.

## Behaviour

- **Recent accounts** live in a cookie `ip_recent` (HttpOnly, SameSite=Lax, 180 days): JSON array of up to 4 `{ login, name, role, lastAt }` entries, most recent first. On every successful login, upsert the current user to the front; on "Bu hesabı siyahıdan sil" (optional, skip if time is short) remove it. Never store the password. Role comes from the user's claims; `lastAt` is UTC.
- **Step 1 (GET /Account/Login, cookie has ≥1 entry):** render the picker. Clicking an account goes to `/Account/Login?user={login}`. "Başqa hesabla daxil ol" goes to `/Account/Login?other=1`.
- **Step 2 (GET with `user` or `other`):** render the password form. With `user`: username is a hidden field, the identity block shows name/login from the cookie entry (if the login is not in the cookie, fall back to the `other` form). With `other`: show both username and password fields, username autofocused.
- **No cookie entries:** go straight to the `other` form (first run on a machine).
- **POST:** unchanged validation. On failure re-render step 2 with the same `user`/`other` state and the error banner. On success set the cookie and redirect to `ReturnUrl` or Dashboard.
- "Məni xatırla" is removed: remembering the account is the cookie; the auth cookie persistence stays whatever it is today.
- Keep `?ReturnUrl=` through both steps (hidden field + query string on the account links).

## Markup

Step 1:
```html
<div class="ip-login">
  <header class="ip-login-head">
    <div class="d-flex align-items-center gap-2"><span class="ip-logo"><i class="fa-solid fa-box-archive"></i></span><strong>Inventory Pro</strong></div>
    <div class="d-flex align-items-center gap-2">[language ip-seg] [theme ip-btn-icon]</div>
  </header>
  <main class="ip-login-main">
    <div class="ip-account-pick">
      <div><h1>@L["Login.Who"]</h1><div class="ip-muted">@L["Login.RecentOnThisPc"]</div></div>
      <div class="ip-account-grid">
        @foreach (var a in Model.Recent) {
        <a class="ip-account" href="@Url.Action("Login", new { user = a.Login, ReturnUrl })">
          <span class="ip-avatar">@a.Initials</span>
          <span class="who"><b>@a.Name</b><small>@a.RoleLabel · @a.LastAtText</small></span>
          <i class="fa-solid fa-chevron-right"></i>
        </a>}
      </div>
      <a class="ip-account-other" href="@Url.Action("Login", new { other = 1, ReturnUrl })"><i class="fa-solid fa-plus"></i>@L["Login.OtherAccount"]</a>
    </div>
  </main>
  <footer class="ip-login-foot"><span>@L["Login.Internal"]</span><span>v@version</span></footer>
</div>
```

Step 2 (inside the same shell, replacing `.ip-account-pick`):
```html
<form class="ip-login-form" method="post">
  @if (Model.Recent.Any()) { <a class="back" href="@Url.Action("Login", new { ReturnUrl })"><i class="fa-solid fa-arrow-left"></i>@L["Login.ChangeAccount"]</a> }
  <div class="identity"><span class="ip-avatar">@Model.Initials</span><div><div class="name">@Model.DisplayName</div><div class="login">@Model.LoginHint</div></div></div>
  [error banner .ip-banner.ip-banner-danger, only on failure: "İstifadəçi adı və ya şifrə yanlışdır."]
  @if (Model.AskUsername) { <div class="ip-field"><label>@L["Username"]</label><input class="ip-input" asp-for="Username" autocomplete="username" autofocus></div> }
  else { <input type="hidden" asp-for="Username"> }
  <div class="ip-field"><label>@L["Password"]</label>
    <div class="ip-pw-wrap"><input class="ip-input" asp-for="Password" type="password" autocomplete="current-password" @(Model.AskUsername ? "" : "autofocus")><button type="button" class="ip-btn ip-btn-icon" data-pw-toggle><i class="fa-regular fa-eye"></i></button></div>
  </div>
  <button class="ip-btn ip-btn-primary" type="submit">@L["Login.Submit"]</button>
  <div class="ip-faint">@L["Login.Forgot"] <a href="mailto:it@sirket.az">it@sirket.az</a></div>
</form>
```
For the `other` form the identity block shows avatar "?" , name "Başqa hesab", hint "İstifadəçi adını daxil edin".

## LastAtText

Relative, in the UI language: "bu gün 08:51", "dünən 18:02", otherwise `dd.MM` of the current year or `dd.MM.yyyy`. Put the helper next to the existing date formatting code.

## Resource keys (AZ / EN)

Login.Who = "Kim daxil olur?" / "Who is signing in?"
Login.RecentOnThisPc = "Bu kompüterdən son girişlər" / "Recent sign-ins on this computer"
Login.OtherAccount = "Başqa hesabla daxil ol" / "Use another account"
Login.ChangeAccount = "Hesabı dəyiş" / "Change account"
Login.Submit = "Daxil ol" / "Sign in"
Login.Forgot = "Şifrəni unutmusunuz? İT şöbəsi sıfırlayır:" / "Forgot your password? IT resets it:"
Login.Internal = "Daxili sistem" / "Internal system"
Login.OtherName = "Başqa hesab" / "Another account"
Login.OtherHint = "İstifadəçi adını daxil edin" / "Enter your username"
Remove the old tagline / RememberMe / "© year" keys.

## Checks

- Fresh browser → username+password form. After login → picker with 1 account. Log in as a second user → 2 cards, newest first.
- Picker card → password-only form with the right name; wrong password → banner, same form; back link returns to picker.
- Cookie never contains a password; max 4 entries; survives theme/language switch.
- 390px wide: cards stack in one column, tap targets ≥44px. Both themes.

Commit as "auth: two-step login with recent-account picker".
