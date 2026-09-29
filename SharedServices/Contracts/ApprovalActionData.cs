using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SharedServices.Contracts
{
    /// <summary>
    /// Readers for stored approval ActionData. Requests written by older versions use both
    /// camelCase and PascalCase and sometimes nest the payload (ProductData / UpdateData), so every
    /// accessor tolerates all of those shapes.
    /// </summary>
    public static class ApprovalActionData
    {
        /// <summary>Returns the nested object <paramref name="name"/> if present, else the element itself.</summary>
        public static JsonElement Section(this JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object) return element;
            return element.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.Object ? nested
                 : element.TryGetProperty(ToCamel(name), out var camel) && camel.ValueKind == JsonValueKind.Object ? camel
                 : element;
        }

        public static bool Has(this JsonElement element, string name)
            => TryGet(element, name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

        public static string GetString(this JsonElement element, string name, string defaultValue = "")
        {
            if (!TryGet(element, name, out var value)) return defaultValue;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? defaultValue,
                JsonValueKind.Null or JsonValueKind.Undefined => defaultValue,
                _ => value.ToString()
            };
        }

        public static int GetInt(this JsonElement element, string name, int defaultValue = 0)
        {
            if (!TryGet(element, name, out var value)) return defaultValue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
            return defaultValue;
        }

        public static bool GetBool(this JsonElement element, string name, bool defaultValue)
        {
            if (!TryGet(element, name, out var value)) return defaultValue;
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                _ => defaultValue
            };
        }

        /// <summary>
        /// Rebuilds the uploaded image stored as base64 ("imageData" + "imageFileName"), or null.
        /// </summary>
        public static IFormFile? GetImage(this JsonElement element)
        {
            var base64 = new[] { "imageData", "image" }
                .Select(n => element.GetString(n))
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));
            var fileName = new[] { "imageFileName", "imageName", "fileName" }
                .Select(n => element.GetString(n))
                .FirstOrDefault(s => !string.IsNullOrEmpty(s));
            if (string.IsNullOrEmpty(base64) || string.IsNullOrEmpty(fileName)) return null;

            var comma = base64.IndexOf(',');
            if (comma >= 0) base64 = base64[(comma + 1)..]; // data-URL prefix

            var bytes = Convert.FromBase64String(base64);
            return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImageFile", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = Path.GetExtension(fileName).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg"
            };
        }

        private static bool TryGet(JsonElement element, string name, out JsonElement value)
        {
            value = default;
            if (element.ValueKind != JsonValueKind.Object) return false;
            return element.TryGetProperty(ToCamel(name), out value) || element.TryGetProperty(ToPascal(name), out value);
        }

        private static string ToCamel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
        private static string ToPascal(string name) => char.ToUpperInvariant(name[0]) + name[1..];
    }
}
