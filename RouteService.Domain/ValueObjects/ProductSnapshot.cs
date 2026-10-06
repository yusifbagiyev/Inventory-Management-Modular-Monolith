namespace RouteService.Domain.ValueObjects
{
    /// <summary>The product's details at the time of the route, stored in the route's own columns.</summary>
    public class ProductSnapshot
    {
        public int ProductId { get; private set; }
        public int InventoryCode { get; private set; }
        public string Model { get; private set; }
        public string Vendor { get; private set; }
        public string CategoryName { get; private set; }
        public bool IsWorking { get; private set; }

        public ProductSnapshot(int productId, int inventoryCode, string model,
            string vendor, string categoryName, bool isWorking)
        {
            ProductId = productId;
            InventoryCode = inventoryCode;
            Model = model;
            Vendor = vendor;
            CategoryName = categoryName;
            IsWorking = isWorking;
        }

        // Changed in place, since EF Core would track a new instance of an owned type as a delete and an insert
        internal void CopyFrom(ProductSnapshot other)
        {
            InventoryCode = other.InventoryCode;
            Model = other.Model;
            Vendor = other.Vendor;
            CategoryName = other.CategoryName;
            IsWorking = other.IsWorking;
        }
    }
}