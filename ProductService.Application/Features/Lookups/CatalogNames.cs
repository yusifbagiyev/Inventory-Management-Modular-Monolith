using ProductService.Domain.Common;
using ProductService.Domain.Repositories;
using SharedServices.Exceptions;
using SharedServices.Services;

namespace ProductService.Application.Features.Lookups
{
    /// <summary>Keeps department and category names unique, since route history and the dashboard tell them apart by name only.</summary>
    public static class CatalogNames
    {
        public static async Task EnsureNameIsFreeAsync(
            this IDepartmentRepository departments, string name, int? exceptId, CancellationToken cancellationToken)
        {
            var taken = FindSameName(await departments.GetLookupAsync(cancellationToken), name, exceptId);
            if (taken != null)
                throw new DuplicateEntityException(taken.IsActive
                    ? $"A department named '{taken.Name}' already exists"
                    : $"A department named '{taken.Name}' already exists but is inactive. Activate it or choose another name.");
        }

        public static async Task EnsureNameIsFreeAsync(
            this ICategoryRepository categories, string name, int? exceptId, CancellationToken cancellationToken)
        {
            var taken = FindSameName(await categories.GetLookupAsync(cancellationToken), name, exceptId);
            if (taken != null)
                throw new DuplicateEntityException(taken.IsActive
                    ? $"A category named '{taken.Name}' already exists"
                    : $"A category named '{taken.Name}' already exists but is inactive. Activate it or choose another name.");
        }

        /// <summary>True when the name differs from the stored one by more than the spaces around either of them.</summary>
        public static bool IsRenamed(string? newName, string currentName)
            => !string.IsNullOrWhiteSpace(newName) && newName.Trim() != currentName.Trim();

        static LookupItem? FindSameName(IEnumerable<LookupItem> items, string name, int? exceptId)
        {
            var key = Key(name);
            return items.FirstOrDefault(i => i.Id != exceptId && Key(i.Name) == key);
        }

        // Case and Azerbaijani letter variants count as the same name, the way search compares text
        static string Key(string name) => SearchHelper.NormalizeForSearch(name.Trim());
    }
}
