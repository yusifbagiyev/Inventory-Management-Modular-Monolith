using RouteService.Domain.Enums;
using RouteService.Domain.Exceptions;
using RouteService.Domain.ValueObjects;

namespace RouteService.Domain.Entities
{
    /// <summary>A transfer or history entry, keeping the product details and department names of that moment.</summary>
    public class InventoryRoute
    {
        public const int WorkerMaxLength = 100;
        public const int NotesMaxLength = 500;

        public int Id { get; private set; }
        public RouteType RouteType { get; private set; }
        public ProductSnapshot ProductSnapshot { get; private set; } = null!;
        public int? FromDepartmentId { get; private set; }
        public string? FromDepartmentName { get; private set; }
        public int ToDepartmentId { get; private set; }
        public string ToDepartmentName { get; private set; } = null!;
        public string? FromWorker { get; private set; }
        public string? ToWorker { get; private set; }
        /// <summary>The cover image, always the first of <see cref="ImageUrls"/>.</summary>
        public string? ImageUrl { get; private set; }
        /// <summary>All images, cover first.</summary>
        public List<string> ImageUrls { get; private set; } = [];
        public string? Notes { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime CompletedAt { get;private set; }

        /// <summary>Queued, Sent or Failed for the transfer's WhatsApp message, null when none was sent.</summary>
        public string? WhatsAppStatus { get; private set; }
        public string? WhatsAppError { get; private set; }
        public DateTime? WhatsAppAt { get; private set; }

        // For EF Core
        protected InventoryRoute() { }

        // A product entering the inventory, new or second-hand
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
                ToWorker = Fit(toWorker, WorkerMaxLength),
                ImageUrl = imageUrl,
                ImageUrls = string.IsNullOrEmpty(imageUrl) ? [] : [imageUrl],
                Notes = Fit(notes, NotesMaxLength),
                IsCompleted = false,
                CreatedAt = DateTime.Now
            };
        }


        // A pending transfer, the product only moves once it is completed
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
                FromWorker = Fit(fromWorker, WorkerMaxLength),
                ToWorker = Fit(toWorker, WorkerMaxLength),
                ImageUrl = imageUrls?.FirstOrDefault(),
                ImageUrls = imageUrls?.ToList() ?? [],
                Notes = Fit(notes, NotesMaxLength),
                IsCompleted = false,
                CreatedAt = DateTime.Now
            };
        }



        // A removal is complete from the start and has no destination department
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
                FromWorker = Fit(fromWorker, WorkerMaxLength),
                ToWorker = Fit(removedBy, WorkerMaxLength),
                Notes = Fit(reason, NotesMaxLength),
                IsCompleted = true,
                CreatedAt = DateTime.Now
            };
        }


        // A product edit, with the department and worker before and after it
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
                FromWorker = Fit(changedProduct.Worker, WorkerMaxLength),
                ToWorker = Fit(worker, WorkerMaxLength),
                ImageUrl = imageUrl,
                ImageUrls = string.IsNullOrEmpty(imageUrl) ? [] : [imageUrl],
                Notes = Fit(notes, NotesMaxLength),
                CreatedAt = DateTime.Now
            };
        }

        /// <summary>Refuses a transfer that would leave the product in its department with the worker it already has.</summary>
        public static void RequireMove(int currentDepartmentId, string? currentWorker, int toDepartmentId, string? toWorker)
        {
            if (toDepartmentId != currentDepartmentId)
                return;

            var worker = (toWorker ?? string.Empty).Trim();
            if (worker.Length == 0)
                throw new RouteException("Enter the new worker to transfer within the same department.");
            if (string.Equals(worker, (currentWorker ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                throw new RouteException("The product is already with this worker. Enter another worker or choose another department.");
        }

        /// <summary>A code change in place, with the new code in the snapshot and the old one in the notes.</summary>
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
                ToWorker = Fit(worker, WorkerMaxLength),
                Notes = Fit(notes, NotesMaxLength),
                CreatedAt = DateTime.Now
            };
        }

        // History text comes from product fields that have no limit, so it is cut to the column instead of failing the save
        private static string? Fit(string? text, int maxLength)
        {
            if (text == null || text.Length <= maxLength)
                return text;

            // A cut between the two halves of a surrogate pair would leave text the database refuses
            var keep = maxLength - 1;
            if (char.IsHighSurrogate(text[keep - 1]))
                keep--;
            return text[..keep] + "…";
        }

        public void SetWhatsAppStatus(string status, string? error)
        {
            WhatsAppStatus = status;
            // The error column holds 500 characters
            WhatsAppError = error is { Length: > 500 } ? error[..500] : error;
            WhatsAppAt = DateTime.Now;
        }

        public void Complete()
        {
            IsCompleted = true;
            CompletedAt = DateTime.Now;
        }

        /// <summary>Replaces the image list, cover first, and keeps <see cref="ImageUrl"/> in step.</summary>
        public void SetImages(IEnumerable<string> imageUrls)
        {
            ImageUrls = imageUrls.ToList();
            ImageUrl = ImageUrls.FirstOrDefault();
        }

        /// <summary>Takes the product's current details and place as the start of a pending transfer.</summary>
        public void RefreshSource(ProductSnapshot product, int fromDepartmentId, string fromDepartmentName, string? fromWorker)
        {
            if (IsCompleted || RouteType != RouteType.Transfer)
                throw new InvalidOperationException("Only a pending transfer follows the product");
            if (product.ProductId != ProductSnapshot.ProductId)
                throw new ArgumentException("The details belong to another product", nameof(product));

            ProductSnapshot.CopyFrom(product);
            FromDepartmentId = fromDepartmentId;
            FromDepartmentName = fromDepartmentName;
            FromWorker = Fit(fromWorker, WorkerMaxLength);
        }

        /// <summary>Names who receives the item, where a blank name means nobody.</summary>
        public void SetToWorker(string? toWorker) => ToWorker = string.IsNullOrWhiteSpace(toWorker) ? null : toWorker.Trim();

        /// <summary>Replaces the notes, where blank text removes them.</summary>
        public void SetNotes(string? notes) => Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        /// <summary>Points a pending route at another destination, keeping the stored id and name in sync.</summary>
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