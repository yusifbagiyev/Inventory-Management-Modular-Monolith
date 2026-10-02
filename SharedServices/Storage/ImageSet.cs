using Microsoft.AspNetCore.Http;

namespace SharedServices.Storage
{
    /// <summary>An item's ordered image list. The first image is the cover and is also kept in ImageUrl.</summary>
    public static class ImageSet
    {
        public const int MaxImages = 10;

        /// <summary>Applies an edit to the list. Returns it with the removed urls, whose files go after commit.</summary>
        public static (List<string> Images, List<string> Removed) Apply(
            IReadOnlyList<string> current,
            IEnumerable<string>? remove,
            IReadOnlyList<string> added,
            string? cover,
            bool replaceAll = false)
        {
            var removeSet = replaceAll ? current.ToHashSet() : (remove ?? []).ToHashSet();
            var removed = current.Where(removeSet.Contains).ToList();
            var images = current.Where(u => !removeSet.Contains(u)).Concat(added).Distinct().ToList();

            if (!string.IsNullOrEmpty(cover) && images.Remove(cover))
                images.Insert(0, cover);

            if (images.Count > MaxImages)
                throw new ArgumentException($"An item can have at most {MaxImages} images");

            return (images, removed);
        }

        /// <summary>Turns the cover into a url. A new upload is sent as new:{index} since it has no url yet.</summary>
        public static string? ResolveCover(string? cover, IReadOnlyList<string> added)
            => cover is not null && cover.StartsWith("new:", StringComparison.Ordinal)
                ? int.TryParse(cover.AsSpan(4), out var i) && i >= 0 && i < added.Count ? added[i] : null
                : cover;

        /// <summary>All non-empty files of a request, the legacy single file first.</summary>
        public static List<IFormFile> Files(IFormFile? single, IEnumerable<IFormFile>? many)
            => new[] { single }.Concat(many ?? []).OfType<IFormFile>().Where(f => f.Length > 0).ToList();

        /// <summary>True when the edit changes the list.</summary>
        public static bool Changes(IReadOnlyList<string> current, IEnumerable<string>? remove, int addedCount, string? cover)
            => addedCount > 0
               || (remove ?? []).Any(current.Contains)
               || (!string.IsNullOrEmpty(cover) && current.Count > 0 && current[0] != cover && current.Contains(cover));
    }
}
