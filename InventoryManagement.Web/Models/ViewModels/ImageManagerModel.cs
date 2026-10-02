namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>Model for the _ImageManager picker, which posts ImageFiles, RemoveImageUrls and CoverImageUrl.</summary>
    public record ImageManagerModel
    {
        /// <summary>Current images with the cover first. Empty on create.</summary>
        public IReadOnlyList<string> Existing { get; init; } = [];
        /// <summary>Null when the section title already says it.</summary>
        public string? Label { get; init; }
        public string? Hint { get; init; }
        public int Max { get; init; } = 10;
    }

    /// <summary>Model for the _ImageGallery partial on details pages.</summary>
    public record ImageGalleryModel
    {
        public IReadOnlyList<string> Images { get; init; } = [];
        public string Title { get; init; } = string.Empty;
        public string EmptyText { get; init; } = "No images";
    }
}
