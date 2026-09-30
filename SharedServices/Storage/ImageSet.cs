using Microsoft.AspNetCore.Http;

namespace SharedServices.Storage
{
    /// <summary>
    /// An item's ordered image list (products and routes). The first image is the cover, which is
    /// also kept in the item's single ImageUrl column for lists, exports and API clients.
    /// </summary>
    public static class ImageSet
    {
        public const int MaxImages = 10;

        /// <summary>
        /// The list after an edit: <paramref name="remove"/> taken out (or everything, when
        /// <paramref name="replaceAll"/>), <paramref name="added"/> appended, then
        /// <paramref name="cover"/> moved to the front if it is in the list.
        /// </summary>
        /// <returns>The new list and the urls that left it (their files are deleted after commit).</returns>
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

        /// <summary>
        /// The cover as a url: a current image's url as sent, or "new:{i}" for the i-th file uploaded
        /// with this edit (the picker can make a new photo the cover before it has a url).
        /// </summary>
        public static string? ResolveCover(string? cover, IReadOnlyList<string> added)
            => cover is not null && cover.StartsWith("new:", StringComparison.Ordinal)
                ? int.TryParse(cover.AsSpan(4), out var i) && i >= 0 && i < added.Count ? added[i] : null
                : cover;

        /// <summary>All non-empty files of a request: the legacy single file first, then the list.</summary>
        public static List<IFormFile> Files(IFormFile? single, IEnumerable<IFormFile>? many)
            => new[] { single }.Concat(many ?? []).OfType<IFormFile>().Where(f => f.Length > 0).ToList();

        /// <summary>True when the edit changes the list (for "what changed" summaries).</summary>
        public static bool Changes(IReadOnlyList<string> current, IEnumerable<string>? remove, int addedCount, string? cover)
            => addedCount > 0
               || (remove ?? []).Any(current.Contains)
               || (!string.IsNullOrEmpty(cover) && current.Count > 0 && current[0] != cover && current.Contains(cover));
    }
}
