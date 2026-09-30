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

        public static IEnumerable<ProductSpecification> ToDomain(IEnumerable<ProductSpecificationDto>? lines)
            => (lines ?? []).Select(l => new ProductSpecification(l.Name ?? "", l.Value ?? ""));

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
