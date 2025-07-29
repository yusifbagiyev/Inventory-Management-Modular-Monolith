namespace InventoryManagement.Web.Models.DTOs
{
    public record UpdateInventoryCodeDto
    {
        public int Id { get; set; }
        public int InventoryCode { get; set; }
    }
}
