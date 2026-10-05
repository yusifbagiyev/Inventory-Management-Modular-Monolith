using SharedServices.Services;

namespace ProductService.Application.DTOs
{
    /// <summary>Column filters and sort of the department and category lists, where a null field means no filter.</summary>
    public record CatalogListFilter
    {
        public string? Sort { get; init; }
        public bool Descending { get; init; }
        public string? Name { get; init; }
        public string? Head { get; init; }
        public string? Description { get; init; }
        public bool[]? Active { get; init; }
        public int? ProductsMin { get; init; }
        public int? ProductsMax { get; init; }
        public int? WorkersMin { get; init; }
        public int? WorkersMax { get; init; }
        public DateTime? CreatedFrom { get; init; }
        public DateTime? CreatedTo { get; init; }
    }

    /// <summary>Filters, sorts and pages the whole department or category list in memory, which stays small.</summary>
    public static class CatalogListing
    {
        public static PagedResultDto<DepartmentDto> Page(
            IEnumerable<DepartmentDto> all, string? search, CatalogListFilter filter, int pageNumber, int pageSize)
        {
            var rows = all.Where(d =>
                    Matches(search, d.Name, d.DepartmentHead, d.Description)
                    && Matches(filter.Name, d.Name)
                    && Matches(filter.Head, d.DepartmentHead)
                    && Matches(filter.Description, d.Description)
                    && InRange(d.WorkerCount ?? 0, filter.WorkersMin, filter.WorkersMax))
                .Where(d => Common(d.IsActive, d.ProductCount ?? 0, d.CreatedAt, filter));

            rows = (filter.Sort ?? "name") switch
            {
                "head" => Order(rows, d => d.DepartmentHead ?? "", filter.Descending),
                "description" => Order(rows, d => d.Description ?? "", filter.Descending),
                "products" => Order(rows, d => d.ProductCount ?? 0, filter.Descending),
                "workers" => Order(rows, d => d.WorkerCount ?? 0, filter.Descending),
                "status" => Order(rows, d => d.IsActive, filter.Descending),
                "created" => Order(rows, d => d.CreatedAt, filter.Descending),
                _ => Order(rows, d => d.Name, filter.Descending)
            };
            return ToPage(rows.ToList(), pageNumber, pageSize);
        }

        public static PagedResultDto<CategoryDto> Page(
            IEnumerable<CategoryDto> all, string? search, CatalogListFilter filter, int pageNumber, int pageSize)
        {
            var rows = all.Where(c =>
                    Matches(search, c.Name, c.Description)
                    && Matches(filter.Name, c.Name)
                    && Matches(filter.Description, c.Description))
                .Where(c => Common(c.IsActive, c.ProductCount ?? 0, c.CreatedAt, filter));

            rows = (filter.Sort ?? "name") switch
            {
                "description" => Order(rows, c => c.Description ?? "", filter.Descending),
                "products" => Order(rows, c => c.ProductCount ?? 0, filter.Descending),
                "status" => Order(rows, c => c.IsActive, filter.Descending),
                "created" => Order(rows, c => c.CreatedAt, filter.Descending),
                _ => Order(rows, c => c.Name, filter.Descending)
            };
            return ToPage(rows.ToList(), pageNumber, pageSize);
        }

        static bool Common(bool isActive, int products, DateTime created, CatalogListFilter filter)
            => (filter.Active is not { Length: > 0 } || filter.Active.Contains(isActive))
               && InRange(products, filter.ProductsMin, filter.ProductsMax)
               && (filter.CreatedFrom == null || created >= filter.CreatedFrom.Value.Date)
               && (filter.CreatedTo == null || created < filter.CreatedTo.Value.Date.AddDays(1));

        // Any of the texts contains the term, folding Azerbaijani letters
        static bool Matches(string? term, params string?[] texts)
            => string.IsNullOrWhiteSpace(term) || texts.Any(t => SearchHelper.ContainsAzerbaijani(t, term.Trim()));

        static bool InRange(int value, int? min, int? max) => (min == null || value >= min) && (max == null || value <= max);

        static IEnumerable<T> Order<T, TKey>(IEnumerable<T> rows, Func<T, TKey> key, bool descending)
        {
            // Text sorts ignore case, the way people read a list
            var comparer = typeof(TKey) == typeof(string)
                ? (IComparer<TKey>)(object)StringComparer.CurrentCultureIgnoreCase
                : Comparer<TKey>.Default;
            return descending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);
        }

        static PagedResultDto<T> ToPage<T>(List<T> rows, int pageNumber, int pageSize)
        {
            pageSize = Math.Max(1, pageSize);
            pageNumber = Math.Max(1, pageNumber);
            return new PagedResultDto<T>
            {
                Items = rows.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                TotalCount = rows.Count,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
    }
}
