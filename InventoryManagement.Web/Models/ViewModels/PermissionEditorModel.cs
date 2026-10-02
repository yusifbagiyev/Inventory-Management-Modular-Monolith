namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>Own is edited while FromRole is shown locked, and without a SaveUrl the editor is read-only.</summary>
    public sealed record PermissionEditorModel(
        IReadOnlySet<string> Own,
        IReadOnlySet<string> FromRole,
        string? SaveUrl,
        IReadOnlyList<(string Name, string? Description)> Others)
    {
        public bool ReadOnly => SaveUrl == null;

        public ISet<string> Effective => Own.Concat(FromRole).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
