using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    public static class PluralExtensions
    {
        /// <summary>
        /// "{0} transfer" for one, "{0} transfers" otherwise (English needs the singular). Both keys are in
        /// az.json / ru.json; those translations are worded to read right with any number.
        /// </summary>
        public static LocalizedString Plural(this IStringLocalizer localizer, int count, string one, string many)
            => localizer[count == 1 ? one : many, count];
    }
}
