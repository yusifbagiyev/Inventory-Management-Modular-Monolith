using Microsoft.Extensions.Configuration;

namespace SharedServices.Storage
{
    /// <summary>
    /// File storage for uploaded images, laid out as {root}/{category}/{inventoryCode}/{file} and
    /// addressed by site-relative URLs /images/{category}/{inventoryCode}/{file}. The root must be
    /// the web root's images folder so the URLs are served as static files.
    /// </summary>
    public sealed class ImageStorage
    {
        public const string Products = "products";
        public const string Routes = "routes";

        private const long MaxBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };

        private readonly string _root;

        public ImageStorage(IConfiguration configuration)
        {
            _root = Path.GetFullPath(configuration["ImageSettings:RootPath"] ?? Path.Combine("wwwroot", "images"));
        }

        public static bool IsAllowedFileName(string fileName)
            => AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

        public async Task<string> SaveAsync(string category, int inventoryCode, Stream content, string fileName, CancellationToken cancellationToken = default)
        {
            if (!IsAllowedFileName(fileName))
                throw new ArgumentException("Invalid image format. Allowed: JPG, JPEG, PNG");
            if (content.CanSeek && content.Length > MaxBytes)
                throw new ArgumentException("Image size exceeds 5MB limit");

            var folder = Path.Combine(_root, category, inventoryCode.ToString());
            Directory.CreateDirectory(folder);

            // Unique per upload: the old tick-based names could collide and overwrite each other.
            var storedName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
            await using (var file = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew))
                await content.CopyToAsync(file, cancellationToken);

            return $"/images/{category}/{inventoryCode}/{storedName}";
        }

        /// <summary>Copies an existing image into another category; null when the source is missing.</summary>
        public async Task<string?> CopyAsync(string? sourceUrl, string targetCategory, int inventoryCode, CancellationToken cancellationToken = default)
        {
            var sourcePath = GetPhysicalPath(sourceUrl);
            if (sourcePath == null || !File.Exists(sourcePath))
                return null;

            await using var source = File.OpenRead(sourcePath);
            return await SaveAsync(targetCategory, inventoryCode, source, Path.GetFileName(sourcePath), cancellationToken);
        }

        public Task DeleteAsync(string? url)
        {
            var path = GetPhysicalPath(url);
            if (path != null && File.Exists(path))
                File.Delete(path);
            return Task.CompletedTask;
        }

        public Task DeleteFolderAsync(string category, int inventoryCode)
        {
            var folder = Path.Combine(_root, category, inventoryCode.ToString());
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            return Task.CompletedTask;
        }

        public async Task<byte[]?> ReadAsync(string? url, CancellationToken cancellationToken = default)
        {
            var path = GetPhysicalPath(url);
            return path != null && File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
        }

        /// <summary>Maps /images/{category}/{code}/{file} to a path under the root, rejecting anything else.</summary>
        public string? GetPhysicalPath(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var segments = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 4 || segments[0] != "images") return null;
            if (segments.Skip(1).Any(s => s is "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) return null;

            var path = Path.GetFullPath(Path.Combine(_root, segments[1], segments[2], segments[3]));
            return path.StartsWith(_root, StringComparison.Ordinal) ? path : null;
        }
    }
}
