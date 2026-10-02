using Microsoft.AspNetCore.Mvc.Rendering;
using ProductService.Domain.Common;

namespace InventoryManagement.Web.Extensions
{
    public static class LookupExtensions
    {
        /// <summary>All items, inactive ones included, for list filters.</summary>
        public static List<SelectListItem> ToSelectList(this IEnumerable<LookupItem> items)
            => items.Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.Name }).ToList();

        /// <summary>Active items plus the current one, so an edit form keeps an inactive value.</summary>
        public static List<SelectListItem> ToChoiceList(this IEnumerable<LookupItem> items, int? currentId = null)
            => items.Where(i => i.IsActive || i.Id == currentId).ToSelectList();
    }
}
