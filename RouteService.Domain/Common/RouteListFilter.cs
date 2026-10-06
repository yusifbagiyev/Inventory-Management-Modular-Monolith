using RouteService.Domain.Enums;

namespace RouteService.Domain.Common
{
    /// <summary>Column filters and sort of the route list, where a null or empty field means no filter.</summary>
    public record RouteListFilter
    {
        /// <summary>date, product, from, to, category, type, status or whatsapp; null keeps the pending-first order.</summary>
        public string? Sort { get; init; }
        public bool Descending { get; init; }
        /// <summary>Models picked from the product column's list.</summary>
        public string[]? Models { get; init; }
        /// <summary>Text matched against the product's inventory code and model.</summary>
        public string? Product { get; init; }
        /// <summary>Department names stored on the route's sending end.</summary>
        public string[]? FromDepartments { get; init; }
        /// <summary>Department names stored on the route's receiving end.</summary>
        public string[]? ToDepartments { get; init; }
        /// <summary>Category names stored in the route's product snapshot.</summary>
        public string[]? Categories { get; init; }
        public RouteType[]? RouteTypes { get; init; }
        /// <summary>Queued, Sent, Failed, or None for routes without a WhatsApp message.</summary>
        public string[]? WhatsApp { get; init; }

        public const string NoWhatsApp = "None";
    }
}
