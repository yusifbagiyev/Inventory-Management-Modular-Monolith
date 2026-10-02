using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Categories.Queries;
using ProductService.Application.Features.Departments.Queries;
using ProductService.Application.Features.Products.Commands;
using ProductService.Application.Features.Products.Queries;
using ProductService.Application.Interfaces;
using ProductService.Domain.Repositories;
using SharedServices.Contracts;
using SharedServices.DTOs;
using SharedServices.Enum;
using SharedServices.Exceptions;
using SharedServices.Identity;
using SharedServices.Storage;
using System.Security.Claims;

namespace ProductService.Application.Services
{
    /// <summary>Runs a product write directly with its .direct permission, otherwise submits it for approval.</summary>
    public class ProductManagementService : IProductManagementService
    {
        private readonly IMediator _mediator;
        private readonly IApprovalRequests _approvalRequests;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly ILogger<ProductManagementService> _logger;

        public ProductManagementService(
            IMediator mediator,
            IApprovalRequests approvalRequests,
            ICategoryRepository categoryRepository,
            IDepartmentRepository departmentRepository,
            ILogger<ProductManagementService> logger)
        {
            _mediator = mediator;
            _approvalRequests = approvalRequests;
            _categoryRepository = categoryRepository;
            _departmentRepository = departmentRepository;
            _logger = logger;
        }

        public async Task<ProductDto> CreateProductWithApprovalAsync(
            CreateProductDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            await ValidateProductDoesNotExist(dto.InventoryCode);

            if (userPermissions.Contains(AllPermissions.ProductCreateDirect))
            {
                _logger.LogInformation($"User {userName} creating product {dto.InventoryCode} directly");
                return await _mediator.Send(new CreateProduct.Command(dto));
            }

            if (!userPermissions.Contains(AllPermissions.ProductCreate))
            {
                throw new InsufficientPermissionsException("You don't have permission to create products");
            }

            var actionData = await BuildCreateProductActionData(dto);
            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.CreateProduct,
                EntityType = "Product",
                EntityId = null,
                ActionData = new 
                {
                    ProductData = actionData
                }
            };

            var requestId = await _approvalRequests.SubmitAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {requestId} created for product {dto.InventoryCode}");
            throw new ApprovalRequiredException(requestId, "Product creation request has been submitted for approval");
        }



        public async Task<ProductDto> UpdateProductWithApprovalAsync(
            int id,
            UpdateProductDto dto,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            var existingProduct = await GetProductById(id);

            var changeComparison = await TrackWhatChanges(existingProduct, dto);

            if (!changeComparison.Any())
            {
                _logger.LogInformation($"No changes detected for product {id}");
                return existingProduct;
            }

            if (userPermissions.Contains(AllPermissions.ProductUpdateDirect))
            {
                _logger.LogInformation($"User {userName} updating product {id} directly");
                await _mediator.Send(new UpdateProduct.Command(id, dto,changeComparison));
                return await GetProductById(id);
            }


            if (!userPermissions.Contains(AllPermissions.ProductUpdate))
            {
                throw new InsufficientPermissionsException("You don't have permission to update products");
            }

            var updateData = await BuildUpdateProductActionData(dto, existingProduct);
            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.UpdateProduct,
                EntityType = "Product",
                EntityId = id,
                ActionData = new
                {
                    ProductId = id,
                    existingProduct.InventoryCode,
                    UpdateData = updateData,
                    Changes = changeComparison
                }
            };

            var requestId = await _approvalRequests.SubmitAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {requestId} created for updating product {id}");
            throw new ApprovalRequiredException(requestId, "Product update request submitted for approval");
        }



        public async Task DeleteProductWithApprovalAsync(
            int id,
            int userId,
            string userName,
            List<string> userPermissions)
        {
            var product = await GetProductById(id);

            if (userPermissions.Contains(AllPermissions.ProductDeleteDirect))
            {
                _logger.LogInformation($"User {userName} deleting product {id} directly");
                await _mediator.Send(new DeleteProduct.Command(id, userName));
                return;
            }

            if (!userPermissions.Contains(AllPermissions.ProductDelete))
            {
                throw new InsufficientPermissionsException("You don't have permission to delete products");
            }

            // The product details let the approver see what would be deleted
            var approvalRequest = new CreateApprovalRequestDto
            {
                RequestType = RequestType.DeleteProduct,
                EntityType = "Product",
                EntityId = id,
                ActionData = new
                {
                    ProductId = id,
                    product.InventoryCode,
                    product.Model,
                    product.Vendor,
                    product.DepartmentName,
                    DeleteReason = $"Requested by {userName}"
                }
            };

            var requestId = await _approvalRequests.SubmitAsync(approvalRequest, userId, userName);

            _logger.LogInformation($"Approval request {requestId} created for deleting product {id}");
            throw new ApprovalRequiredException(requestId, "Product deletion request submitted for approval");
        }



        private async Task<ProductDto> GetProductById(int id)
        {
            return await _mediator.Send(new GetProductByIdQuery(id)) ??
                throw new NotFoundException($"Product with ID {id} not found");
        }



        private async Task ValidateProductDoesNotExist(int inventoryCode)
        {
            var existingProduct = await _mediator.Send(new GetProductByInventoryCodeQuery(inventoryCode));

            if (existingProduct != null)
            {
                _logger.LogWarning($"Attempt to create duplicate product with inventory code {inventoryCode}");
                throw new DuplicateEntityException($"Product with inventory code {inventoryCode} already exists");
            }
        }



        private async Task<Dictionary<string, object>> BuildCreateProductActionData(CreateProductDto dto)
        {
            var actionData = new Dictionary<string, object>
            {
                ["inventoryCode"] = dto.InventoryCode,
                ["model"] = dto.Model ?? "",
                ["vendor"] = dto.Vendor ?? "",
                ["worker"] = dto.Worker ?? "",
                ["description"] = dto.Description ?? "",
                ["isWorking"] = dto.IsWorking,
                ["isActive"] = dto.IsActive,
                ["isNewItem"] = dto.IsNewItem,
                ["categoryId"] = dto.CategoryId,
                ["departmentId"] = dto.DepartmentId,
                ["color"] = dto.Color ?? "",
                ["specifications"] = SpecificationData(dto.Specifications)
            };

            // Names make the request readable for the approver, but a failed lookup isn't fatal
            try
            {
                var category = await _mediator.Send(new GetCategoryByIdQuery(dto.CategoryId));
                var department = await _mediator.Send(new GetDepartmentByIdQuery(dto.DepartmentId));

                actionData["categoryName"] = category?.Name ?? $"Category #{dto.CategoryId}";
                actionData["departmentName"] = department?.Name ?? $"Department #{dto.DepartmentId}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich product data with names");
            }

            var images = ImageSet.Files(dto.ImageFile, dto.ImageFiles);
            if (images.Count > 0)
                actionData["images"] = await ApprovalActionData.EncodeImagesAsync(images);

            return actionData;
        }



        private async Task<Dictionary<string, object>> BuildUpdateProductActionData(UpdateProductDto dto, ProductDto existing)
        {
            var updateData = new Dictionary<string, object>
            {
                ["model"] = dto.Model ?? "",
                ["vendor"] = dto.Vendor ?? "",
                ["worker"] = dto.Worker ?? "",
                ["description"] = dto.Description ?? "",
                ["categoryId"] = dto.CategoryId,
                ["departmentId"] = dto.DepartmentId,
                ["isWorking"] = dto.IsWorking,
                ["isActive"] = dto.IsActive,
                ["isNewItem"] = dto.IsNewItem,
            };
            if (dto.ReplaceDetails)
            {
                updateData["replaceDetails"] = true;
                updateData["color"] = dto.Color ?? "";
                updateData["specifications"] = SpecificationData(dto.Specifications);
            }

            // A single legacy ImageFile still means replace all images
            if (dto.ImageFile is { Length: > 0 })
                updateData["replaceImages"] = await ApprovalActionData.EncodeImagesAsync([dto.ImageFile]);
            if (dto.ImageFiles?.Any(f => f.Length > 0) == true)
                updateData["images"] = await ApprovalActionData.EncodeImagesAsync(dto.ImageFiles);
            if (dto.RemoveImageUrls?.Count > 0)
                updateData["removeImageUrls"] = dto.RemoveImageUrls;
            if (!string.IsNullOrEmpty(dto.CoverImageUrl))
                updateData["coverImageUrl"] = dto.CoverImageUrl;

            // Approval applies only the changed fields, so it can't undo a transfer made while the request waited
            updateData["changed"] = ChangedFields(existing, dto);

            return updateData;
        }

        /// <summary>Request data keys of the changed fields, where details covers colour and specifications.</summary>
        private static List<string> ChangedFields(ProductDto existing, UpdateProductDto dto)
        {
            var changed = new List<string>();
            if (TextDiffers(existing.Model, dto.Model)) changed.Add("model");
            if (TextDiffers(existing.Vendor, dto.Vendor)) changed.Add("vendor");
            if (TextDiffers(existing.Worker, dto.Worker)) changed.Add("worker");
            if (TextDiffers(existing.Description, dto.Description)) changed.Add("description");
            if (existing.CategoryId != dto.CategoryId) changed.Add("categoryId");
            if (existing.DepartmentId != dto.DepartmentId) changed.Add("departmentId");
            if (existing.IsWorking != dto.IsWorking) changed.Add("isWorking");
            if (existing.IsActive != dto.IsActive) changed.Add("isActive");
            if (existing.IsNewItem != dto.IsNewItem) changed.Add("isNewItem");
            if (dto.ReplaceDetails
                && (TextDiffers(existing.Color, dto.Color)
                    || !existing.Specifications.Select(s => (s.Name ?? "", s.Value ?? ""))
                        .SequenceEqual(SpecificationData(dto.Specifications).Select(s => (s["name"], s["value"])))))
                changed.Add("details");
            return changed;
        }



        private static List<Dictionary<string, string>> SpecificationData(IEnumerable<ProductSpecificationDto>? lines)
            => (lines ?? [])
                .Where(l => !string.IsNullOrWhiteSpace(l.Name))
                .Select(l => new Dictionary<string, string> { ["name"] = l.Name!.Trim(), ["value"] = (l.Value ?? "").Trim() })
                .ToList();

        public async Task<List<string>> TrackWhatChanges(ProductDto existingProduct, UpdateProductDto updatedProduct)
        {
            ArgumentNullException.ThrowIfNull(existingProduct);
            ArgumentNullException.ThrowIfNull(updatedProduct);

            var changes = new List<string>();
            if (TextDiffers(existingProduct.Vendor, updatedProduct.Vendor))
                changes.Add($"Vendor: {existingProduct.Vendor} → {updatedProduct.Vendor}");
            if (TextDiffers(existingProduct.Model, updatedProduct.Model))
                changes.Add($"Model: {existingProduct.Model} → {updatedProduct.Model}");
            if (existingProduct.CategoryId != updatedProduct.CategoryId)
                changes.Add($"Category: {existingProduct.CategoryName} → {await GetCategoryNameAsync(updatedProduct.CategoryId)}");
            if (existingProduct.DepartmentId != updatedProduct.DepartmentId)
                changes.Add($"Department: {existingProduct.DepartmentName} → {await GetDepartmentNameAsync(updatedProduct.DepartmentId)}");
            if (TextDiffers(existingProduct.Worker, updatedProduct.Worker))
                changes.Add($"Worker: {existingProduct.Worker ?? "None"} → {updatedProduct.Worker ?? "None"}");
            if (TextDiffers(existingProduct.Description, updatedProduct.Description))
                changes.Add($"Description: {existingProduct.Description} → {updatedProduct.Description}");
            if (existingProduct.IsNewItem != updatedProduct.IsNewItem)
                changes.Add(updatedProduct.IsNewItem == true ? "Product is new now" : "Product's status changed to old");
            if (existingProduct.IsActive != updatedProduct.IsActive)
                changes.Add(updatedProduct.IsActive == true ? "Product is active now" : "Product is not available");
            if (existingProduct.IsWorking != updatedProduct.IsWorking)
                changes.Add(updatedProduct.IsWorking == true ? "Product is working now" : "Product is not working ");
            if (updatedProduct.ReplaceDetails)
            {
                if (TextDiffers(existingProduct.Color, updatedProduct.Color))
                    changes.Add($"Color: {(string.IsNullOrWhiteSpace(existingProduct.Color) ? "None" : existingProduct.Color)} → {(string.IsNullOrWhiteSpace(updatedProduct.Color) ? "None" : updatedProduct.Color!.Trim())}");
                var current = existingProduct.Specifications.Select(s => (s.Name ?? "", s.Value ?? ""));
                var proposed = SpecificationData(updatedProduct.Specifications).Select(s => (s["name"], s["value"]));
                if (!current.SequenceEqual(proposed))
                    changes.Add("Specifications were updated");
            }
            if (ImageSet.Changes(existingProduct.ImageUrls, updatedProduct.RemoveImageUrls,
                    ImageSet.Files(updatedProduct.ImageFile, updatedProduct.ImageFiles).Count, updatedProduct.CoverImageUrl))
                changes.Add("Product images were updated");

            return changes;
        }



        /// <summary>Treats null and empty as equal, since form posts send an empty string for a null value.</summary>
        private static bool TextDiffers(string? current, string? updated)
            => !string.Equals((current ?? "").Trim(), (updated ?? "").Trim(), StringComparison.Ordinal);

        public async Task<string?> GetCategoryNameAsync(int categoryId)
        {
            var categoryName = await _categoryRepository.GetByIdAsync(categoryId)
                ?? throw new NotFoundException($"Category was not found with ID: {categoryId}");
            return categoryName?.Name;
        }



        public async Task<string?> GetDepartmentNameAsync(int departmentId)
        {
            var departmentName = await _departmentRepository.GetByIdAsync(departmentId)
                ?? throw new NotFoundException($"Department was not found with ID: {departmentId}");
            return departmentName.Name;
        }



        public int GetUserId(ClaimsPrincipal User)
        {
            // API-key clients carry a service id, so they get 0 and the permission check answers 403, not a 500
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        }



        public string GetUserName(ClaimsPrincipal User)
        {
            return User.Identity?.Name ?? "Unknown";
        }



        public List<string> GetUserPermissions(ClaimsPrincipal User)
        {
            return User.Claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();
        }
    }
}