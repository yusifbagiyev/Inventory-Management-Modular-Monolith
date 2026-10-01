using System.Globalization;
using System.Text;
using System.Text.Json;
using InventoryManagement.Web.Localization;

namespace InventoryManagement.Web.Services
{
    /// <summary>
    /// The accounts that signed in on this browser, for the sign-in page's account picker: an
    /// HttpOnly cookie with up to <see cref="Max"/> entries (login, name, role, last sign-in),
    /// newest first. Never a password. Anyone at the same computer sees these names, so each card
    /// can be removed from the list.
    /// </summary>
    public static class RecentAccounts
    {
        public const string Cookie = "ip_recent";
        public const int Max = 4;
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

        public sealed record Entry(string Login, string Name, string Role, DateTime LastAt);

        public static List<Entry> Read(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue(Cookie, out var raw) || string.IsNullOrEmpty(raw))
                return [];
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(raw.Replace('-', '+').Replace('_', '/').PadRight((raw.Length + 3) / 4 * 4, '=')));
                return (JsonSerializer.Deserialize<List<Entry>>(json) ?? [])
                    .Where(e => !string.IsNullOrWhiteSpace(e.Login))
                    .Take(Max)
                    .ToList();
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                return [];   // an old or tampered cookie is simply ignored
            }
        }

        /// <summary>Puts the account first (replacing an older entry for the same login).</summary>
        public static void Remember(HttpContext context, Entry entry)
        {
            var list = Read(context.Request);
            list.RemoveAll(e => string.Equals(e.Login, entry.Login, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, entry);
            Write(context, list.Take(Max).ToList());
        }

        public static void Forget(HttpContext context, string login)
        {
            var list = Read(context.Request);
            list.RemoveAll(e => string.Equals(e.Login, login, StringComparison.OrdinalIgnoreCase));
            Write(context, list);
        }

        private static void Write(HttpContext context, List<Entry> list)
        {
            if (list.Count == 0)
            {
                context.Response.Cookies.Delete(Cookie);
                return;
            }
            var value = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            context.Response.Cookies.Append(Cookie, value, new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(Lifetime),
                IsEssential = true
            });
        }

        /// <summary>"AB" from "Anar Babayev"; the login's first letter when there is no name.</summary>
        public static string Initials(string name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var letters = parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : name.Length > 0 ? name[..1] : "?";
            return letters.ToUpper(CultureInfo.CurrentUICulture);
        }

        /// <summary>When the account last signed in: "today 08:51", "yesterday 18:02", else dd.MM (this year) or dd.MM.yyyy.</summary>
        public static string LastAtText(DateTime lastAtUtc)
        {
            var local = DateTime.SpecifyKind(lastAtUtc, DateTimeKind.Utc).ToLocalTime();
            var today = DateTime.Now.Date;
            if (local.Date == today)
                return $"{Word("Today")} {local:HH:mm}";
            if (local.Date == today.AddDays(-1))
                return $"{Word("Yesterday")} {local:HH:mm}";
            return local.Year == today.Year ? local.ToString("dd.MM") : local.ToString("dd.MM.yyyy");
        }

        /// <summary>"Today" -> "today" / "bu gün", for use mid-sentence.</summary>
        private static string Word(string key) => JsonStringLocalizer.TranslateMessage(key).ToLower(CultureInfo.CurrentUICulture);
    }
}
