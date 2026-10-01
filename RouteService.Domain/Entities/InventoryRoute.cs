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
        /// <summary>The cover image: always the first of <see cref="ImageUrls"/>.</summary>
        public string? ImageUrl { get; private set; }
        /// <summary>All images, cover first.</summary>
        public List<string> ImageUrls { get; private set; } = [];
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
                FromDepartmentId = null,
                FromDepartmentName = null,
                ToDepartmentId = toDepartmentId,
                ToDepartmentName = toDepartmentName,
                FromWorker = null,
                ToWorker = toWorker,
                ImageUrl = imageUrl,
                ImageUrls = string.IsNullOrEmpty(imageUrl) ? [] : [imageUrl],
                Notes = notes,
                IsCompleted = false,
                CreatedAt = DateTime.Now
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
            IReadOnlyList<string>? imageUrls = null,
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
                ImageUrl = imageUrls?.FirstOrDefault(),
                ImageUrls = imageUrls?.ToList() ?? [],
                Notes = notes,
                IsCompleted = false,
                CreatedAt = DateTime.Now
            };
        }



        // For removing from inventory
        public static InventoryRoute CreateRemoval(
            ProductSnapshot productSnapshot,
            int fromDepartmentId,
            string fromDepartmentName,
            string fromWorker,
            string removedBy,
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
                ToWorker = removedBy,
                Notes = reason,
                IsCompleted = true,
                CreatedAt = DateTime.Now
            };
        }


        public static InventoryRoute CreateUpdate(
            ExistingProduct changedProduct,
            ProductSnapshot updatedProduct,
            int departmentId,
            string departmentName,
            string? worker,
            string? imageUrl,
            string notes)
        {
            return new InventoryRoute
            {
                RouteType = RouteType.Update,
                ProductSnapshot = updatedProduct,
                FromDepartmentId = changedProduct.DepartmentId,
                FromDepartmentName = changedProduct.DepartmentName,
                ToDepartmentId = departmentId,
                ToDepartmentName = departmentName,
                FromWorker = changedProduct.Worker,
                ToWorker = worker,
                ImageUrl = imageUrl,
                ImageUrls = string.IsNullOrEmpty(imageUrl) ? [] : [imageUrl],
                Notes = notes,
                CreatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// A change of the product's inventory code. The snapshot holds the new code; the old one is
        /// in the notes. The product stays where it is, so there is no "from" place.
        /// </summary>
        public static InventoryRoute CreateCodeChange(
            ProductSnapshot productSnapshot,
            int departmentId,
            string departmentName,
            string? worker,
            string notes)
        {
            return new InventoryRoute
            {
                RouteType = RouteType.CodeChange,
                ProductSnapshot = productSnapshot,
                ToDepartmentId = departmentId,
                ToDepartmentName = departmentName,
                ToWorker = worker,
                Notes = notes,
                CreatedAt = DateTime.Now
            };
        }

        public void Complete()
        {
            IsCompleted = true;
            CompletedAt = DateTime.Now;
        }

        /// <summary>Replaces the image list (cover first) and keeps <see cref="ImageUrl"/> in step.</summary>
        public void SetImages(IEnumerable<string> imageUrls)
        {
            ImageUrls = imageUrls.ToList();
            ImageUrl = ImageUrls.FirstOrDefault();
        }
        public void UpdateExistingRoute(string? toWorker,string? notes)
        {
            ToWorker= toWorker;
            Notes = notes;
        }

        /// <summary>
        /// Re-points a pending route at a different destination. Id and name are kept in sync so the
        /// denormalised name can never drift from the id it describes.
        /// </summary>
        public void UpdateDestination(int departmentId, string departmentName)
        {
            if (departmentId <= 0)
                throw new ArgumentException("Destination department id must be positive", nameof(departmentId));
            if (string.IsNullOrWhiteSpace(departmentName))
                throw new ArgumentException("Destination department name is required", nameof(departmentName));

            ToDepartmentId = departmentId;
            ToDepartmentName = departmentName;
        }
    }
}