using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InventoryManagement.Web.Services
{
    /// <summary>Maps module DTOs to view models through a Newtonsoft JToken, keeping JSON semantics like case-insensitive names.</summary>
    public static class ModelMapper
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault();

        public static T Map<T>(object source) => JToken.FromObject(source, Serializer).ToObject<T>(Serializer)!;

        public static List<T> MapList<T>(IEnumerable<object> source) => source.Select(Map<T>).ToList();
    }
}
