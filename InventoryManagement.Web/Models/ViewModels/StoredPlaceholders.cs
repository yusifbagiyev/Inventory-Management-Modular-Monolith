using System.Diagnostics.CodeAnalysis;
using InventoryManagement.Web.Localization;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>English words the modules store where a value is missing, translated when they are shown.</summary>
    public static class StoredPlaceholders
    {
        public const string NoName = "No Name";
        public const string NoWorker = "No Worker";
        public const string Removed = "Removed";

        /// <summary>A model or vendor, which holds "No Name" when the product was saved without one.</summary>
        [return: NotNullIfNotNull(nameof(value))]
        public static string? Name(string? value) => value == NoName ? JsonStringLocalizer.TranslateMessage(NoName) : value;

        /// <summary>The worker a removed product was taken from, which holds "No Worker" when it had none.</summary>
        [return: NotNullIfNotNull(nameof(value))]
        public static string? Worker(string? value) => value == NoWorker ? JsonStringLocalizer.TranslateMessage(NoWorker) : value;

        /// <summary>A route's destination, which for a removal is no department but the word "Removed".</summary>
        public static string Destination(int toDepartmentId, string name)
            => toDepartmentId == 0 && name == Removed ? JsonStringLocalizer.TranslateMessage(Removed) : name;
    }
}
