using Microsoft.Extensions.Configuration;
using ProductService.Application.Interfaces;

namespace ProductService.Infrastructure.Services
{
    public class ImageService : IImageService
    {
        private readonly string _imagePath;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png"};

        public ImageService(IConfiguration configuration)
        {
            _imagePath = configuration.GetSection("ImageSettings:ImagePath").Value ?? "wwwroot/images/products";
            Directory.CreateDirectory(_imagePath);
        }

        public async Task<string> UploadImageAsync(Stream imageStream, string fileName,int inventoryCode)
        {
            if (!IsValidImage(fileName))
                throw new ArgumentException("Invalid image format");

            var uniqueFileName = $"{inventoryCode}-{DateTime.UtcNow.Ticks}{Path.GetExtension(fileName)}";
            var filePath = Path.Combine(_imagePath, uniqueFileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await imageStream.CopyToAsync(fileStream);

            return $"/images/products/{uniqueFileName}";
        }

        public Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return Task.CompletedTask;

            var fileName = Path.GetFileName(imageUrl);
            var filePath = Path.Combine(_imagePath, fileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }

        public bool IsValidImage(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }
    }
}
