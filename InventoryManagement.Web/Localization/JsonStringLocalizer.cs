using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    /// <summary>Marker type for <c>IStringLocalizer&lt;SharedResource&gt;</c>: the whole UI shares one table.</summary>
    public sealed class SharedResource;

    /// <summary>
    /// UI translations. The English text is the key; <c>Resources/i18n/{language}.json</c> (embedded:
    /// az, ru) maps it to that language. The same table is handed to the browser (<c>window.I18n</c>)
    /// so views and scripts translate from one file. Anything missing falls back to the English key.
    /// </summary>
    public sealed partial class JsonStringLocalizer : IStringLocalizer
    {
        /// <summary>One table per translated language (English needs none: the keys are English).</summary>
        private static readonly Dictionary<string, Lazy<Dictionary<string, string>>> Tables = new()
        {
            ["az"] = new(() => Load("i18n.az.json")),
            ["ru"] = new(() => Load("i18n.ru.json")),
        };

        /// <summary>Per language: keys with {0}-style placeholders turned into regexes, for translating runtime messages.</summary>
        private static readonly Dictionary<string, Lazy<List<(Regex Pattern, string Template)>>> PatternsByLanguage =
            Tables.ToDictionary(t => t.Key, t => new Lazy<List<(Regex Pattern, string Template)>>(() => BuildPatterns(t.Value.Value)));

        /// <summary>The interface language: "az", "ru" or "en".</summary>
        public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        public static bool IsAzerbaijani => Language == "az";
        public static bool IsRussian => Language == "ru";

        /// <summary>The current language's table; null for English.</summary>
        private static Dictionary<string, string>? Table => Tables.TryGetValue(Language, out var t) ? t.Value : null;

        private static List<(Regex Pattern, string Template)> Patterns
            => PatternsByLanguage.TryGetValue(Language, out var p) ? p.Value : [];

        public static IReadOnlyDictionary<string, string> CurrentTable => TableFor(Language);

        /// <summary>A language's table ("az", "ru"); empty for English or an unknown code.</summary>
        public static IReadOnlyDictionary<string, string> TableFor(string? language)
            => language != null && Tables.TryGetValue(language, out var t) ? t.Value : new Dictionary<string, string>();

        /// <summary>Changes whenever a translation file does; cache-busts the script that ships it to the browser.</summary>
        public static string Version => VersionHash.Value;

        private static readonly Lazy<string> VersionHash = new(() => Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                Tables.OrderBy(t => t.Key).Select(t => t.Value.Value).ToList())))[..12].ToLowerInvariant());

        public LocalizedString this[string name]
        {
            get
            {
                name ??= string.Empty;
                var found = TryGet(name, out var value);
                return new LocalizedString(name, value, resourceNotFound: !found);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                name ??= string.Empty;
                var found = TryGet(name, out var value);
                return new LocalizedString(name, string.Format(CultureInfo.CurrentCulture, value, arguments), !found);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => CurrentTable.Select(p => new LocalizedString(p.Key, p.Value, false));

        /// <summary>
        /// Translates a message produced at runtime (exception, validation error) that may carry values,
        /// e.g. "Product with inventory code 1042 already exists" against the key
        /// "Product with inventory code {0} already exists". Untranslated messages are returned as-is.
        /// </summary>
        public static string TranslateMessage(string? message)
        {
            var table = Table;
            if (string.IsNullOrEmpty(message) || table == null)
                return message ?? string.Empty;

            if (table.TryGetValue(message, out var exact))
                return exact;

            // Several messages joined by "; " (validation errors).
            if (message.Contains("; "))
                return string.Join("; ", message.Split("; ").Select(TranslateMessage));

            foreach (var (pattern, template) in Patterns)
            {
                var match = pattern.Match(message);
                if (!match.Success)
                    continue;
                // Captured values can be translatable text themselves (e.g. a ", "-joined change list).
                var values = match.Groups.Cast<Group>().Skip(1).Select(g => (object)TranslateParts(g.Value)).ToArray();
                return string.Format(CultureInfo.CurrentCulture, template, values);
            }

            // A ", "-joined list of known messages (change summaries).
            return message.Contains(", ") ? TranslateParts(message) : message;
        }

        /// <summary>One level down: exact keys and patterns for each ", "-separated part (no further nesting).</summary>
        private static string TranslateParts(string value)
            => string.Join(", ", SplitItems(value).Select(part =>
            {
                var table = Table!;
                if (table.TryGetValue(part, out var exact) || table.TryGetValue(part.Trim(), out exact))
                    return exact;
                foreach (var (pattern, template) in Patterns)
                {
                    var m = pattern.Match(part);
                    if (m.Success)
                        return string.Format(CultureInfo.CurrentCulture, template, m.Groups.Cast<Group>().Skip(1).Select(g => (object)Value(g.Value)).ToArray());
                }
                return part;
            }));

        /// <summary>
        /// Splits a ", "-joined change list into its items. A piece that is not a known message on
        /// its own belongs to the item before it: values contain commas too ("Description: a, b → c").
        /// </summary>
        private static List<string> SplitItems(string value)
        {
            var items = new List<string>();
            foreach (var piece in value.Split(", "))
            {
                var known = (Table?.ContainsKey(piece.Trim()) ?? false) || Patterns.Any(p => p.Pattern.IsMatch(piece))
                    || StartsItem.IsMatch(piece);
                if (items.Count > 0 && !known)
                    items[^1] += ", " + piece;
                else
                    items.Add(piece);
            }
            return items;
        }

        /// <summary>"Worker: ...", "Description: ..." - the start of a "Field: old → new" item.</summary>
        private static readonly Regex StartsItem = new(@"^[A-Z][A-Za-z ]{1,30}: ", RegexOptions.Compiled);

        /// <summary>A value inside a change: only the "None" placeholder for an empty value is translated.</summary>
        private static string Value(string value)
            => value == "None" && Table is { } table && table.TryGetValue("None", out var none) ? none : value;

        private static bool TryGet(string name, out string value)
        {
            if (Table is { } table && table.TryGetValue(name, out var translated))
            {
                value = translated;
                return true;
            }
            value = name;
            return false;
        }

        private static Dictionary<string, string> Load(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded translation file '{resourceName}' is missing.");
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? [];
        }

        private static List<(Regex Pattern, string Template)> BuildPatterns(Dictionary<string, string> table)
        {
            var list = new List<(Regex Pattern, string Template)>();
            foreach (var (key, value) in table)
            {
                // A pattern needs real words around its placeholders, or it would match unrelated text.
                // The "Field: old → new" change lines are specific through the arrow, so short
                // field names ("Worker", "Model") still count.
                var literal = Placeholder().Replace(key, "");
                if (!Placeholder().IsMatch(key) || (literal.Count(char.IsLetter) < 8 && !literal.Contains('→')))
                    continue;
                // Placeholders in the key become capture groups, in index order ({0} first).
                var order = Placeholder().Matches(key).Select(m => int.Parse(m.Groups[1].Value)).ToList();
                var regex = "^" + string.Concat(Placeholder().Split(key)
                    .Select((part, i) => i % 2 == 0 ? Regex.Escape(part) : "(.*?)")) + "$";
                // Rewrite the template so {n} refers to the n-th captured group.
                var template = Placeholder().Replace(value, m => "{" + order.IndexOf(int.Parse(m.Groups[1].Value)) + "}");
                list.Add((new Regex(regex, RegexOptions.CultureInvariant), template));
            }
            // Most specific (longest literal text) first.
            return list.OrderByDescending(p => p.Item1.ToString().Length).ToList();
        }

        [GeneratedRegex(@"\{(\d+)\}")]
        private static partial Regex Placeholder();
    }

    public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private static readonly JsonStringLocalizer Instance = new();
        public IStringLocalizer Create(Type resourceSource) => Instance;
        public IStringLocalizer Create(string baseName, string location) => Instance;
    }
}
