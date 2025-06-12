namespace ProductService.Application.Interfaces
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(Stream imageStream, string fileName,int inventoryCode);
        Task DeleteImageAsync(string imageUrl);
        Task DeleteInventoryFolderAsync(int inventoryCode);
        bool IsValidImage(string fileName);
    }
}