using SharedServices.Storage;
using SkiaSharp;

namespace InventoryManagement.Web.Services
{
    /// <summary>
    /// Small copies of uploaded photos for lists: /thumbs/{width}/images/products/... serves the
    /// photo scaled to fit width x width (JPEG), made on first request and kept on disk under
    /// {ImageSettings:RootPath}/_thumbs. Lists showed 44-56px squares from originals of 100 KB-1 MB.
    /// Photos never change under the same name (each upload gets a unique one), so a thumbnail is
    /// cached by the browser for a year.
    /// </summary>
    public sealed class ImageThumbnails
    {
        public static readonly int[] Widths = [160, 480];
        private const int Quality = 80;

        private readonly string _root;
        private readonly ILogger<ImageThumbnails> _logger;
        /// <summary>Decoding is memory-heavy: at most two thumbnails are made at a time.</summary>
        private static readonly SemaphoreSlim Rendering = new(2);

        public ImageThumbnails(IConfiguration configuration, ILogger<ImageThumbnails> logger)
        {
            _root = Path.GetFullPath(configuration["ImageSettings:RootPath"] ?? Path.Combine("wwwroot", "images"));
            _logger = logger;
        }

        /// <summary>The thumbnail URL for an uploaded image URL ("/images/..."); anything else unchanged.</summary>
        public static string? Url(string? imageUrl, int width = 160)
            => imageUrl != null && imageUrl.StartsWith("/images/", StringComparison.Ordinal) ? $"/thumbs/{width}{imageUrl}" : imageUrl;

        /// <summary>The thumbnail file for images/{relativePath}, made if needed; null when the photo is missing or unreadable.</summary>
        public async Task<string?> GetAsync(int width, string relativePath, CancellationToken cancellationToken)
        {
            if (!Widths.Contains(width)) return null;

            // Only uploaded photos, and nothing outside the images root.
            var source = Path.GetFullPath(Path.Combine(_root, relativePath));
            if (!source.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !(relativePath.StartsWith("products/") || relativePath.StartsWith("routes/"))
                || !File.Exists(source))
                return null;

            var target = Path.Combine(_root, "_thumbs", width.ToString(), relativePath) + ".jpg";
            if (File.Exists(target)) return target;
            // A photo that could not be made into a thumbnail is not tried again on every request.
            var failed = target + ".failed";
            if (File.Exists(failed)) return null;

            await Rendering.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(target)) return target;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var bytes = await Task.Run(() => Render(source, width), cancellationToken);
                if (bytes == null)
                {
                    await File.WriteAllBytesAsync(failed, [], cancellationToken);
                    return null;
                }
                // Write aside and move, so a request arriving meanwhile never reads half a file.
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
            // Older uploads were not checked (ImageSanitizer): refuse what would unpack to gigabytes.
            if ((long)codec.Info.Width * codec.Info.Height > ImageSanitizer.MaxPixels) return null;

            // JPEGs decode straight at 1/2, 1/4 or 1/8 size: a 12 MP photo for a 160 px thumbnail
            // need not be unpacked whole.
            var scale = Math.Min(1f, 2f * width / Math.Max(codec.Info.Width, codec.Info.Height));
            var size = codec.GetScaledDimensions(scale);
            using var decoded = SKBitmap.Decode(codec, codec.Info.WithSize(size.Width, size.Height))
                ?? SKBitmap.Decode(codec);   // formats that cannot decode scaled (PNG)
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
