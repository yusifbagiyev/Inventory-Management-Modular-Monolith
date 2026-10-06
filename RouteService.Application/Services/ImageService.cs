using RouteService.Application.Interfaces;
using SharedServices.Storage;

namespace RouteService.Application.Services
{
    public class ImageService : IImageService
    {
        private readonly ImageStorage _storage;

        public ImageService(ImageStorage storage)
        {
            _storage = storage;
        }

        public Task<string> UploadImageAsync(Stream imageStream, string fileName, int inventoryCode)
            => _storage.SaveAsync(ImageStorage.Routes, inventoryCode, imageStream, fileName);

        public Task DeleteImageAsync(string imageUrl) => _storage.DeleteAsync(imageUrl);
    }
}
