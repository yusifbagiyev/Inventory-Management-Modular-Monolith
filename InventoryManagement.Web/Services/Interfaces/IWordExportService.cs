using InventoryManagement.Web.Models.ViewModels;

namespace InventoryManagement.Web.Services.Interfaces
{
    public interface IWordExportService
    {
        byte[] GenerateDepartmentInventoryDocument(
            DepartmentViewModel department,
            List<ProductViewModel> products);
    }
}