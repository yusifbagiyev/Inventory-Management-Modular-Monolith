namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>
    /// The multi-image picker (Views/Shared/_ImageManager.cshtml + wwwroot/js/image-manager.js).
    /// It posts <c>ImageFiles</c> (new files, in order), <c>RemoveImageUrls</c> and <c>CoverImageUrl</c>.
    /// </summary>
    public record ImageManagerModel
    {
        /// <summary>Current images, cover first (empty on create).</summary>
        public IReadOnlyList<string> Existing { get; init; } = [];
        /// <summary>Shown above the tiles; null when the surrounding section title already says it.</summary>
        public string? Label { get; init; }
        public string? Hint { get; init; }
        public int Max { get; init; } = 10;
    }

    /// <summary>The image gallery on details pages (Views/Shared/_ImageGallery.cshtml).</summary>
    public record ImageGalleryModel
    {
        public IReadOnlyList<string> Images { get; init; } = [];
        public string Title { get; init; } = string.Empty;
        public string EmptyText { get; init; } = "No images";
    }
}
