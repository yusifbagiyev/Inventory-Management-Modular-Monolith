using RouteService.Domain.Enums;
using RouteService.Domain.ValueObjects;

namespace RouteService.Domain.Entities
{
    public class InventoryRoute
    {
        public int Id { get; private set; }
        public RouteType RouteType { get; private set; }
        public ProductSnapshot ProductSnapshot { get; private set; } = null!;
        public int? FromDepartmentId { get; private set; }
        public string? FromDepartmentName { get; private set; }
        public int ToDepartmentId { get; private set; }
        public string ToDepartmentName { get; private set; } = null!;
        public string? FromWorker { get; private set; }
        public string? ToWorker { get; private set; }
        public string? ImageUrl { get; private set; }
        public string? Notes { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime CompletedAt { get;private set; }

        //For EF Core
        protected InventoryRoute() { }

        // For new inventory (new or existing item)
        public static InventoryRoute CreateNewInventory(
            ProductSnapshot productSnapshot,
            int toDepartmentId,
            string toDepartmentName,
            string? toWorker,
            bool isNewItem,
            string? imageUrl = null,
            string? notes = null)
        {
            return new InventoryRoute
            {
                RouteType = isNewItem ? RouteType.New : RouteType.Existing,
                ProductSnapshot = productSnapshot,
                FromDepartmentId=null,
                FromDepartmentName = null,
                ToDepartmentId = toDepartmentId,
                ToDepartmentName = toDepartmentName,
                FromWorker = null,
                ToWorker = toWorker,
                ImageUrl = imageUrl,
                Notes = notes,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };
        }


        // For transfers between departments
        public static InventoryRoute CreateTransfer(
            ProductSnapshot productSnapshot,
            int fromDepartmentId,
            string fromDepartmentName,
            int toDepartmentId,
            string toDepartmentName,
            string? fromWorker,
            string? toWorker,
            string? imageUrl = null,
            string? notes = null)
        {
            return new InventoryRoute
            {
                RouteType = RouteType.Transfer,
                ProductSnapshot = productSnapshot,
                FromDepartmentId = fromDepartmentId,
                FromDepartmentName = fromDepartmentName,
                ToDepartmentId = toDepartmentId,
                ToDepartmentName = toDepartmentName,
                FromWorker = fromWorker,
                ToWorker = toWorker,
                ImageUrl = imageUrl,
                Notes = notes,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };
        }



        // For removing from inventory
        public static InventoryRoute CreateRemoval(
            ProductSnapshot productSnapshot,
            int fromDepartmentId,
            string fromDepartmentName,
            string fromWorker,
            string reason)
        {
            return new InventoryRoute
            {
                RouteType = RouteType.Removal,
                ProductSnapshot = productSnapshot,
                FromDepartmentId = fromDepartmentId,
                FromDepartmentName = fromDepartmentName,
                ToDepartmentId = 0, // No destination for removal
                ToDepartmentName = "Removed",
                FromWorker = fromWorker,
                ToWorker = "System",
                Notes = reason,
                IsCompleted = true,
                CreatedAt = DateTime.UtcNow
            };
        }


        public static InventoryRoute CreateUpdate(
            ProductSnapshot productSnapshot,
            int departmentId,
            string departmentName,
            string? worker,
            string notes)
        {
            return new InventoryRoute
            {
                RouteType = RouteType.Update,
                ProductSnapshot = productSnapshot,
                FromDepartmentId = departmentId, // No previous department for updates
                FromDepartmentName = departmentName,
                ToDepartmentId = departmentId,
                ToDepartmentName = departmentName,
                FromWorker = worker,
                ToWorker = worker,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };
        }

        public void Complete()
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
        }

        public void UpdateImage(string imageUrl)
        {
            ImageUrl = imageUrl;
        }
        public void UpdateNotes(string notes)
        {
            Notes = notes;
        }
    }
}