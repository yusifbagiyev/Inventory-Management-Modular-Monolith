using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventoryManagement.Web.Services
{
    /// <summary>
    /// Maps module DTOs onto the UI's view models by property name. The view models used to be
    /// deserialized from the backend's JSON with Newtonsoft; mapping through a JToken keeps exactly
    /// those semantics (case-insensitive names, enums as numbers, lenient conversions) now that
    /// the DTOs arrive in-process.
    /// </summary>
    public static class ModelMapper
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault();

        public static T Map<T>(object source) => JToken.FromObject(source, Serializer).ToObject<T>(Serializer)!;

        public static List<T> MapList<T>(IEnumerable<object> source) => source.Select(Map<T>).ToList();
    }
}
