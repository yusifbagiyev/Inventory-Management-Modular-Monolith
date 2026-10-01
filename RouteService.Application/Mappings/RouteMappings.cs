using RouteService.Application.DTOs;
using RouteService.Domain.Entities;

namespace RouteService.Application.Mappings
{
    public static class RouteMappings
    {
        public static InventoryRouteDto ToDto(this InventoryRoute route) => new()
        {
            Id = route.Id,
            RouteType = route.RouteType,
            ProductId = route.ProductSnapshot.ProductId,
            InventoryCode = route.ProductSnapshot.InventoryCode,
            Model = route.ProductSnapshot.Model,
            Vendor = route.ProductSnapshot.Vendor,
            CategoryName = route.ProductSnapshot.CategoryName,
            FromDepartmentId = route.FromDepartmentId,
            FromDepartmentName = route.FromDepartmentName,
            ToDepartmentId = route.ToDepartmentId,
            ToDepartmentName = route.ToDepartmentName,
            FromWorker = route.FromWorker,
            ToWorker = route.ToWorker ?? string.Empty,
            ImageUrl = route.ImageUrl,
            ImageUrls = route.ImageUrls.ToList(),
            Notes = route.Notes,
            IsCompleted = route.IsCompleted,
            CreatedAt = route.CreatedAt,
            CompletedAt = route.CompletedAt,
            WhatsAppStatus = route.WhatsAppStatus,
            WhatsAppError = route.WhatsAppError,
            WhatsAppAt = route.WhatsAppAt
        };
    }
}
