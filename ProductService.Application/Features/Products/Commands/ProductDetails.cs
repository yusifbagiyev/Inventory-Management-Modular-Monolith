using ProductService.Application.DTOs;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using SharedServices.Exceptions;

namespace ProductService.Application.Features.Products.Commands
{
    /// <summary>Shared by the create and update commands.</summary>
    internal static class ProductDetails
    {
        public const int MaxSpecifications = 30;

        // Model and vendor are the column sizes, and the worker must also fit the history row written with every change
        public const int MaxModelLength = 50;
        public const int MaxVendorLength = 30;
        public const int MaxWorkerLength = 100;
        public const int MaxDescriptionLength = 500;

        /// <summary>Starts the change line that holds the old and the new description.</summary>
        public const string DescriptionChange = "Description: ";

        /// <summary>Leaves room for the opening words of the history note, which holds 500 characters in all.</summary>
        public const int MaxChangeSummaryLength = 450;

        public static IEnumerable<ProductSpecification> ToDomain(IEnumerable<ProductSpecificationDto>? lines)
            => (lines ?? []).Select(l => new ProductSpecification(l.Name ?? "", l.Value ?? ""));

        /// <summary>Joins the change lines into the text of the history note, shortened when it would not fit there.</summary>
        public static string ChangeSummary(IReadOnlyCollection<string> changes)
        {
            var summary = string.Join(", ", changes);
            if (summary.Length <= MaxChangeSummaryLength)
                return summary;

            // The audit log keeps both versions of the description, so the note can do without them
            summary = string.Join(", ", changes.Select(change =>
                change.StartsWith(DescriptionChange, StringComparison.Ordinal) ? "Description was updated" : change));
            if (summary.Length <= MaxChangeSummaryLength)
                return summary;

            // Half of a surrogate pair is not valid text, so the cut moves in front of the pair
            var cut = MaxChangeSummaryLength - 1;
            if (char.IsHighSurrogate(summary[cut - 1]))
                cut--;
            return summary[..cut] + "…";
        }

        /// <summary>New assignments go to active departments only.</summary>
        public static async Task RequireActiveDepartmentAsync(IDepartmentRepository departments, int departmentId, CancellationToken cancellationToken)
        {
            var department = await departments.GetByIdAsync(departmentId, cancellationToken)
                ?? throw new NotFoundException($"Department with ID {departmentId} not found");
            if (!department.IsActive)
                throw new InvalidOperationException($"The department {department.Name} is inactive. Choose an active department.");
        }
    }
}
