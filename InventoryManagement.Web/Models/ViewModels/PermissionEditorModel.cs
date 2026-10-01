namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>
    /// The permission editor (_PermissionEditor partial): <see cref="Own"/> is what is edited (a
    /// user's own permissions, or a role's), <see cref="FromRole"/> what the user already holds
    /// through their role (shown, locked). No <see cref="SaveUrl"/>: read-only (the profile page).
    /// </summary>
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
