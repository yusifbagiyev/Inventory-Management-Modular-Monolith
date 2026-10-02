using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    public static class PluralExtensions
    {
        /// <summary>Picks the singular key for one, which only English needs. The translations read right with any number.</summary>
        public static LocalizedString Plural(this IStringLocalizer localizer, int count, string one, string many)
            => localizer[count == 1 ? one : many, count];
    }
}
