using Microsoft.AspNetCore.Mvc.Rendering;
using ProductService.Domain.Common;

namespace InventoryManagement.Web.Extensions
{
    public static class LookupExtensions
    {
        /// <summary>Every item: list filters, where records in an inactive department still exist.</summary>
        public static List<SelectListItem> ToSelectList(this IEnumerable<LookupItem> items)
            => items.Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.Name }).ToList();

        /// <summary>
        /// Items that can be chosen for a record: the active ones, plus <paramref name="currentId"/> when
        /// the record already points at an inactive one (so an edit form keeps its value).
        /// </summary>
        public static List<SelectListItem> ToChoiceList(this IEnumerable<LookupItem> items, int? currentId = null)
            => items.Where(i => i.IsActive || i.Id == currentId).ToSelectList();
    }
}
