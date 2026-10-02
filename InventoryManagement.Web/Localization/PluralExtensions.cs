using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    public static class PluralExtensions
    {
        /// <summary>Picks the singular key for a count of one, which only English needs.</summary>
        public static LocalizedString Plural(this IStringLocalizer localizer, int count, string one, string many)
            => localizer[count == 1 ? one : many, count];
    }
}
