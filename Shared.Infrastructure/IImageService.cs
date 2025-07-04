namespace Shared.Infrastructure
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(Stream imageStream, string fileName, int inventoryCode);
        Task DeleteImageAsync(string imageUrl);
        bool IsValidImage(string fileName);
    }
}