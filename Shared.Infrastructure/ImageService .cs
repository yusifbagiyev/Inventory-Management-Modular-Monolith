using Microsoft.Extensions.Configuration;

namespace Shared.Infrastructure
{
    public class ImageService : IImageService
    {
        private readonly string _imagePath;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly long _maxSizeInBytes;

        public ImageService(IConfiguration configuration, string imagePathKey)
        {
            _imagePath = configuration[$"{imagePathKey}:Path"] ?? "wwwroot/images";
            _maxSizeInBytes = (configuration.GetValue<int>($"{imagePathKey}:MaxSizeInMB", 5)) * 1024 * 1024;
            Directory.CreateDirectory(_imagePath);
        }

        public async Task<string> UploadImageAsync(Stream imageStream, string fileName, int inventoryCode)
        {
            ValidateImage(imageStream, fileName);

            var inventoryFolder = Path.Combine(_imagePath, inventoryCode.ToString());
            Directory.CreateDirectory(inventoryFolder);

            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(inventoryFolder, uniqueFileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await imageStream.CopyToAsync(fileStream);

            return $"/images/{Path.GetFileName(_imagePath)}/{inventoryCode}/{uniqueFileName}";
        }

        private void ValidateImage(Stream imageStream, string fileName)
        {
            if (!IsValidImage(fileName))
                throw new ArgumentException($"Invalid image format. Allowed formats: {string.Join(", ", _allowedExtensions)}");

            if (imageStream.Length > _maxSizeInBytes)
                throw new ArgumentException($"Image size exceeds {_maxSizeInBytes / (1024 * 1024)}MB limit");
        }

        public async Task DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return;

            try
            {
                var segments = imageUrl.Split('/');
                if (segments.Length >= 2)
                {
                    var inventoryCode = segments[^2];
                    var fileName = segments[^1];
                    var filePath = Path.Combine(_imagePath, inventoryCode, fileName);

                    if (File.Exists(filePath))
                    {
                        await Task.Run(() => File.Delete(filePath));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - image deletion is not critical
                // Add logging here
            }
        }

        public async Task DeleteInventoryFolderAsync(int inventoryCode)
        {
            var folderPath = Path.Combine(_imagePath, inventoryCode.ToString());

            if (Directory.Exists(folderPath))
            {
                await Task.Run(() => Directory.Delete(folderPath, true));
            }
        }

        public bool IsValidImage(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return !string.IsNullOrEmpty(extension) && _allowedExtensions.Contains(extension);
        }
    }
}