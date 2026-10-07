using System.Diagnostics;
using SharedServices.Storage;
using SkiaSharp;

namespace InventoryManagement.Web.Services
{
    /// <summary>One-off job that strips EXIF data from photos saved before ImageSanitizer, keeping their names so URLs work.</summary>
    public sealed class PhotoMetadataCleanup : BackgroundService
    {
        private const string Marker = ".metadata-removed-v1";

        private readonly string _root;
        private readonly ILogger<PhotoMetadataCleanup> _logger;

        public PhotoMetadataCleanup(IConfiguration configuration, ILogger<PhotoMetadataCleanup> logger)
        {
            _root = Path.GetFullPath(configuration["ImageSettings:RootPath"] ?? Path.Combine("wwwroot", "images"));
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // The marker file in _thumbs keeps the job from running twice
            var marker = Path.Combine(_root, ImageStorage.Thumbnails, Marker);

            try
            {
                if (File.Exists(marker)) return;

                // Let startup and the warm-up queries go first
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                var watch = Stopwatch.StartNew();
                int cleaned = 0, skipped = 0;

                foreach (var category in new[] { ImageStorage.Products, ImageStorage.Routes })
                {
                    var folder = Path.Combine(_root, category);
                    if (!Directory.Exists(folder)) continue;

                    foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        stoppingToken.ThrowIfCancellationRequested();
                        if (!ImageStorage.IsAllowedFileName(path)) continue;
                        try
                        {
                            var data = await File.ReadAllBytesAsync(path, stoppingToken);
                            if (!ImageSanitizer.HasMetadata(data)) continue;

                            var clean = ImageSanitizer.Clean(data).Data;
                            // Keep the original when the cleaned bytes no longer decode
                            using (var check = SKCodec.Create(new SKMemoryStream(clean)))
                                if (check == null) { skipped++; continue; }

                            // Write beside the photo and move it over so a crash never leaves a half-written file
                            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            await File.WriteAllBytesAsync(temp, clean, stoppingToken);
                            File.Move(temp, path, overwrite: true);
                            cleaned++;
                        }
                        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
                        {
                            skipped++;
                            _logger.LogWarning("Photo metadata not removed from {Path}: {Reason}", path, ex.Message);
                        }
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                await File.WriteAllTextAsync(marker, $"{DateTime.Now:o} cleaned {cleaned}, skipped {skipped}", stoppingToken);
                _logger.LogInformation("Photo metadata removed from {Cleaned} photos ({Skipped} skipped) in {Elapsed} ms",
                    cleaned, skipped, watch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The marker is only written on completion, so it runs again on the next start
            }
            catch (Exception ex)
            {
                // An exception leaving a background service stops the whole host, and this job is optional
                _logger.LogError(ex, "Photo metadata cleanup stopped early and will run again on the next start");
            }
        }
    }
}
