using Microsoft.AspNetCore.Mvc.Rendering;
using ProductService.Domain.Common;

namespace InventoryManagement.Web.Extensions
{
    public static class LookupExtensions
    {
        public static List<SelectListItem> ToSelectList(this IEnumerable<LookupItem> items)
            => items.Select(i => new SelectListItem { Value = i.Id.ToString(), Text = i.Name }).ToList();
    }
}
