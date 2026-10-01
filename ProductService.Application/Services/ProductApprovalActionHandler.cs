using System.Text.Json;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using SharedServices.Contracts;
using SharedServices.Enum;
using SharedServices.Exceptions;

namespace ProductService.Application.Services
{
    /// <summary>Executes approved product create/update/delete requests in-process.</summary>
    public class ProductApprovalActionHandler : IApprovalActionHandler
    {
        private readonly IMediator _mediator;
        private readonly IProductManagementService _productManagement;

        public ProductApprovalActionHandler(IMediator mediator, IProductManagementService productManagement)
        {
            _mediator = mediator;
            _productManagement = productManagement;
        }

        public bool CanHandle(string requestType) =>
            requestType is RequestType.CreateProduct or RequestType.UpdateProduct or RequestType.DeleteProduct;

        public Task ExecuteAsync(string requestType, JsonElement actionData, ApprovalActor approver, CancellationToken cancellationToken) =>
            requestType switch
            {
                RequestType.CreateProduct => CreateAsync(actionData.Section("ProductData"), cancellationToken),
                RequestType.UpdateProduct => UpdateAsync(actionData, cancellationToken),
                RequestType.DeleteProduct => DeleteAsync(actionData, approver, cancellationToken),
                _ => throw new NotSupportedException($"Request type '{requestType}' is not handled by the products module")
            };

        private Task CreateAsync(JsonElement data, CancellationToken cancellationToken)
        {
            var dto = new CreateProductDto
            {
                InventoryCode = data.GetInt("inventoryCode"),
                Model = data.GetString("model"),
                Vendor = data.GetString("vendor"),
                Worker = data.GetString("worker"),
                Description = data.GetString("description"),
                IsWorking = data.GetBool("isWorking", true),
                IsActive = data.GetBool("isActive", true),
                IsNewItem = data.GetBool("isNewItem", true),
                CategoryId = data.GetInt("categoryId"),
                DepartmentId = data.GetInt("departmentId"),
                Color = data.GetString("color"),
                Specifications = Specifications(data),
                ImageFile = data.GetImage(),
                ImageFiles = data.GetImages()
            };
            return _mediator.Send(new CreateProduct.Command(dto), cancellationToken);
        }

        private async Task UpdateAsync(JsonElement root, CancellationToken cancellationToken)
        {
            var productId = RequireId(root, "productId");
            var data = root.Section("UpdateData");

            var dto = new UpdateProductDto
            {
                Model = data.GetString("model"),
                Vendor = data.GetString("vendor"),
                Worker = data.GetString("worker"),
                Description = data.GetString("description"),
                CategoryId = data.GetInt("categoryId"),
                DepartmentId = data.GetInt("departmentId"),
                IsWorking = data.GetBool("isWorking", true),
                IsActive = data.GetBool("isActive", true),
                IsNewItem = data.GetBool("isNewItem", true),
                // Older requests carry one "imageData" image, which replaced the image.
                ImageFile = data.GetImage() ?? data.GetImages("replaceImages").FirstOrDefault(),
                ImageFiles = data.GetImages(),
                RemoveImageUrls = data.GetStrings("removeImageUrls"),
                CoverImageUrl = data.Has("coverImageUrl") ? data.GetString("coverImageUrl") : null,
                // Requests made before colour/specifications existed leave them as they are.
                ReplaceDetails = data.GetBool("replaceDetails", false),
                Color = data.GetString("color"),
                Specifications = Specifications(data)
            };

            var existing = await _mediator.Send(new GetProductByIdQuery(productId), cancellationToken)
                ?? throw new NotFoundException($"Product with ID {productId} not found");

            // Requests that list their changed fields apply only those, on top of the product as
            // it is now (it may have been transferred or edited while the request waited). Older
            // requests carry no list and apply every field, as before.
            if (data.Has("changed"))
            {
                var changed = data.GetStrings("changed").ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!changed.Contains("model")) dto.Model = existing.Model;
                if (!changed.Contains("vendor")) dto.Vendor = existing.Vendor;
                if (!changed.Contains("worker")) dto.Worker = existing.Worker;
                if (!changed.Contains("description")) dto.Description = existing.Description;
                if (!changed.Contains("categoryId")) dto.CategoryId = existing.CategoryId;
                if (!changed.Contains("departmentId")) dto.DepartmentId = existing.DepartmentId;
                if (!changed.Contains("isWorking")) dto.IsWorking = existing.IsWorking;
                if (!changed.Contains("isActive")) dto.IsActive = existing.IsActive;
                if (!changed.Contains("isNewItem")) dto.IsNewItem = existing.IsNewItem;
                if (!changed.Contains("details")) dto.ReplaceDetails = false;
            }

            var changes = await _productManagement.TrackWhatChanges(existing, dto);
            if (changes.Count > 0)
                await _mediator.Send(new UpdateProduct.Command(productId, dto, changes), cancellationToken);
        }

        private Task DeleteAsync(JsonElement root, ApprovalActor approver, CancellationToken cancellationToken)
            => _mediator.Send(new DeleteProduct.Command(RequireId(root, "productId"), approver.UserName), cancellationToken);

        private static List<ProductSpecificationDto> Specifications(JsonElement data)
            => data.GetObjects("specifications")
                .Select(s => new ProductSpecificationDto { Name = s.GetString("name"), Value = s.GetString("value") })
                .ToList();

        private static int RequireId(JsonElement element, string name)
        {
            var id = element.GetInt(name);
            return id > 0 ? id : throw new InvalidOperationException($"'{name}' is missing from the request data");
        }
    }
}
