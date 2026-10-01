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

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var bytes = await Task.Run(() => Render(source, width), cancellationToken);
                if (bytes == null) return null;
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
        }

        private static byte[]? Render(string source, int width)
        {
            using var stream = File.OpenRead(source);
            using var codec = SKCodec.Create(stream);
            if (codec == null) return null;
            using var decoded = SKBitmap.Decode(codec);
            if (decoded == null) return null;
            using var upright = Orient(decoded, codec.EncodedOrigin);

            var scale = Math.Min(1f, width / (float)Math.Max(upright.Width, upright.Height));
            var info = new SKImageInfo(Math.Max(1, (int)(upright.Width * scale)), Math.Max(1, (int)(upright.Height * scale)));
            using var resized = upright.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell)) ?? upright.Copy();
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
            return data?.ToArray();
        }

        /// <summary>Applies the photo's EXIF orientation (phones store pictures sideways plus a flag).</summary>
        private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft) return bitmap.Copy();

            var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            var result = new SKBitmap(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
            using var canvas = new SKCanvas(result);
            switch (origin)
            {
                case SKEncodedOrigin.TopRight:      // mirrored
                    canvas.Translate(result.Width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight:   // upside down
                    canvas.Translate(result.Width, result.Height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft:    // flipped
                    canvas.Translate(0, result.Height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop:      // rotated 90 degrees (the usual phone portrait)
                case SKEncodedOrigin.LeftTop:
                    canvas.Translate(result.Width, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.LeftBottom:    // rotated 270 degrees
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(0, result.Height); canvas.RotateDegrees(270); break;
            }
            canvas.DrawBitmap(bitmap, 0, 0);
            return result;
        }
    }
}
