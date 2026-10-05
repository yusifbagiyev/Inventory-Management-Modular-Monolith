using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using SharedServices.Services;

namespace SharedServices.Persistence
{
    /// <summary>Azerbaijani-aware text matching inside database queries, folding letters like SearchHelper does in memory.</summary>
    public static class SearchSql
    {
        // Same pairs as SearchHelper.NormalizeForSearch, while ILIKE takes care of the case
        const string From = "ƏəĞğÜüŞşıİÖöÇç";
        const string To = "EeGgUuSsiIOoCc";

        /// <summary>The text with Azerbaijani letters folded to ASCII, usable only inside a query.</summary>
        public static string Fold(string? text) => throw new NotSupportedException("SearchSql.Fold runs only inside a database query.");

        /// <summary>A LIKE pattern that finds the folded term anywhere, with its own wildcards taken literally.</summary>
        public static string Contains(string term)
            => "%" + SearchHelper.NormalizeForSearch(term.Trim()).Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";

        /// <summary>Maps Fold to PostgreSQL translate(), called from a module context's OnModelCreating.</summary>
        public static void AddSearchFold(this ModelBuilder modelBuilder)
        {
            modelBuilder.HasDbFunction(typeof(SearchSql).GetMethod(nameof(Fold))!)
                .HasTranslation(args => new SqlFunctionExpression(
                    "translate",
                    new SqlExpression[] { args[0], new SqlFragmentExpression($"'{From}'"), new SqlFragmentExpression($"'{To}'") },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, false, false },
                    typeof(string),
                    args[0].TypeMapping));
        }
    }
}
