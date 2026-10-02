using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    /// <summary>Marker type so the whole UI shares one localizer table.</summary>
    public sealed class SharedResource;

    /// <summary>Translates by English key from the embedded i18n json files, which the browser also gets.</summary>
    public sealed partial class JsonStringLocalizer : IStringLocalizer
    {
        // English has no table because the keys are English
        private static readonly Dictionary<string, Lazy<Dictionary<string, string>>> Tables = new()
        {
            ["az"] = new(() => Load("i18n.az.json")),
            ["ru"] = new(() => Load("i18n.ru.json")),
        };

        // Keys with placeholders turned into regexes, used to translate runtime messages
        private static readonly Dictionary<string, Lazy<List<(Regex Pattern, string Template)>>> PatternsByLanguage =
            Tables.ToDictionary(t => t.Key, t => new Lazy<List<(Regex Pattern, string Template)>>(() => BuildPatterns(t.Value.Value)));

        public static string Language => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        public static bool IsAzerbaijani => Language == "az";
        public static bool IsRussian => Language == "ru";

        // Null for English
        private static Dictionary<string, string>? Table => Tables.TryGetValue(Language, out var t) ? t.Value : null;

        private static List<(Regex Pattern, string Template)> Patterns
            => PatternsByLanguage.TryGetValue(Language, out var p) ? p.Value : [];

        public static IReadOnlyDictionary<string, string> CurrentTable => TableFor(Language);

        /// <summary>Empty for English or an unknown language code.</summary>
        public static IReadOnlyDictionary<string, string> TableFor(string? language)
            => language != null && Tables.TryGetValue(language, out var t) ? t.Value : new Dictionary<string, string>();

        /// <summary>Hash of the translation files, used to cache-bust the strings script.</summary>
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

        /// <summary>Translates a runtime message with values in it by matching it against the placeholder keys.</summary>
        public static string TranslateMessage(string? message)
        {
            var table = Table;
            if (string.IsNullOrEmpty(message) || table == null)
                return message ?? string.Empty;

            if (table.TryGetValue(message, out var exact))
                return exact;

            // Validation errors come joined into one message
            if (message.Contains("; "))
                return string.Join("; ", message.Split("; ").Select(TranslateMessage));

            foreach (var (pattern, template) in Patterns)
            {
                var match = pattern.Match(message);
                if (!match.Success)
                    continue;
                // A captured value can itself be translatable text, such as a change list
                var values = match.Groups.Cast<Group>().Skip(1).Select(g => (object)TranslateParts(g.Value)).ToArray();
                return string.Format(CultureInfo.CurrentCulture, template, values);
            }

            // Change summaries are comma-joined lists of known messages
            return message.Contains(", ") ? TranslateParts(message) : message;
        }

        /// <summary>Translates each part of a comma-joined list, one level deep only.</summary>
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

        /// <summary>Splits a change list into items, gluing an unknown piece to the one before since values can hold commas.</summary>
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

        // Matches the field name that starts a change item
        private static readonly Regex StartsItem = new(@"^[A-Z][A-Za-z ]{1,30}: ", RegexOptions.Compiled);

        // Inside a change only the None placeholder is translated, real values stay as they are
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
                // Without 8 letters of real text or an arrow a pattern would match unrelated messages
                var literal = Placeholder().Replace(key, "");
                if (!Placeholder().IsMatch(key) || (literal.Count(char.IsLetter) < 8 && !literal.Contains('→')))
                    continue;
                var order = Placeholder().Matches(key).Select(m => int.Parse(m.Groups[1].Value)).ToList();
                var regex = "^" + string.Concat(Placeholder().Split(key)
                    .Select((part, i) => i % 2 == 0 ? Regex.Escape(part) : "(.*?)")) + "$";
                // Placeholders may appear out of order in the key, so renumber them by capture position
                var template = Placeholder().Replace(value, m => "{" + order.IndexOf(int.Parse(m.Groups[1].Value)) + "}");
                list.Add((new Regex(regex, RegexOptions.CultureInvariant), template));
            }
            // Longest pattern first so the most specific one wins
            return list.OrderByDescending(p => p.Item1.ToString().Length).ToList();
        }

        [GeneratedRegex(@"\{(\d+)\}")]
        private static partial Regex Placeholder();
    }

    /// <summary>Hands out the one shared localizer whatever resource type is asked for.</summary>
    public sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private static readonly JsonStringLocalizer Instance = new();
        public IStringLocalizer Create(Type resourceSource) => Instance;
        public IStringLocalizer Create(string baseName, string location) => Instance;
    }
}
