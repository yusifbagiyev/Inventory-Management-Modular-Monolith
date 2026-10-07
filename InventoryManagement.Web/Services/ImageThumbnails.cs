using SharedServices.Storage;
using SkiaSharp;

namespace InventoryManagement.Web.Services
{
    /// <summary>List thumbnails made on first request under _thumbs, never stale since every upload has a unique name.</summary>
    public sealed class ImageThumbnails
    {
        public static readonly int[] Widths = [160, 480];
        private const int Quality = 80;

        private readonly string _root;
        private readonly ILogger<ImageThumbnails> _logger;
        // Decoding is memory-heavy, so at most two thumbnails are made at a time
        private static readonly SemaphoreSlim Rendering = new(2);

        public ImageThumbnails(IConfiguration configuration, ILogger<ImageThumbnails> logger)
        {
            _root = Path.GetFullPath(configuration["ImageSettings:RootPath"] ?? Path.Combine("wwwroot", "images"));
            _logger = logger;
        }

        /// <summary>Thumbnail URL for an uploaded image, any other URL comes back unchanged.</summary>
        public static string? Url(string? imageUrl, int width = 160)
            => imageUrl != null && imageUrl.StartsWith("/images/", StringComparison.Ordinal) ? $"/thumbs/{width}{imageUrl}" : imageUrl;

        /// <summary>Returns the thumbnail path, or null when the photo is missing or unreadable.</summary>
        public async Task<string?> GetAsync(int width, string relativePath, CancellationToken cancellationToken)
        {
            if (!Widths.Contains(width)) return null;

            // Uploaded photos only, and never a path that escapes the images root
            var source = Path.GetFullPath(Path.Combine(_root, relativePath));
            if (!source.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !(relativePath.StartsWith("products/") || relativePath.StartsWith("routes/"))
                || !File.Exists(source))
                return null;

            var target = Path.Combine(_root, ImageStorage.Thumbnails, width.ToString(), relativePath) + ".jpg";
            if (File.Exists(target)) return target;
            // A marker file keeps a broken photo from being retried on every request
            var failed = target + ".failed";
            if (File.Exists(failed)) return null;

            await Rendering.WaitAsync(cancellationToken);
            try
            {
                // Another request may have made it while this one waited
                if (File.Exists(target)) return target;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var bytes = await Task.Run(() => Render(source, width), cancellationToken);
                if (bytes == null)
                {
                    await File.WriteAllBytesAsync(failed, [], cancellationToken);
                    return null;
                }
                // Write to a temp file and move it so a parallel request never reads half a file
                var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
                File.Move(temp, target, overwrite: true);
                return target;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Thumbnail for {Path} could not be saved", relativePath);
                return null;
            }
            finally
            {
                Rendering.Release();
            }
        }

        private static byte[]? Render(string source, int width)
        {
            using var stream = File.OpenRead(source);
            using var codec = SKCodec.Create(stream);
            if (codec == null) return null;
            // Older uploads skipped ImageSanitizer, so refuse anything that would unpack to gigabytes
            if ((long)codec.Info.Width * codec.Info.Height > ImageSanitizer.MaxPixels) return null;

            // JPEGs can decode straight at 1/2, 1/4 or 1/8 size, which saves unpacking the whole photo
            var scale = Math.Min(1f, 2f * width / Math.Max(codec.Info.Width, codec.Info.Height));
            var size = codec.GetScaledDimensions(scale);
            using var decoded = SKBitmap.Decode(codec, codec.Info.WithSize(size.Width, size.Height))
                ?? SKBitmap.Decode(codec);   // PNG cannot decode scaled
            if (decoded == null) return null;
            var oriented = ImageSanitizer.Orient(decoded, codec.EncodedOrigin);
            using var upright = ReferenceEquals(oriented, decoded) ? null : oriented;
            var picture = upright ?? decoded;

            var fit = Math.Min(1f, width / (float)Math.Max(picture.Width, picture.Height));
            var info = new SKImageInfo(Math.Max(1, (int)(picture.Width * fit)), Math.Max(1, (int)(picture.Height * fit)));
            using var resized = picture.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? picture.Copy();
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
            return data?.ToArray();
        }
    }
}
