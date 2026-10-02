using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>
    /// The sign-in page: with accounts remembered on this browser it first shows them
    /// (<see cref="ShowPicker"/>); a picked account then asks only for the password
    /// (<see cref="Mode"/> "user"), "use another account" asks for both ("other").
    /// </summary>
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(SharedServices.Identity.PasswordRules.MaxUsernameLength)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(SharedServices.Identity.PasswordRules.MaxPasswordLength)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        /// <summary>"user": the account picked from the list (username is a hidden field); "other": both fields.</summary>
        public string? Mode { get; set; }

        public string? ReturnUrl { get; set; }

        [BindNever] public bool ShowPicker { get; set; }
        [BindNever] public IReadOnlyList<RecentAccountView> Recent { get; set; } = [];
        [BindNever] public string DisplayName { get; set; } = string.Empty;
        [BindNever] public string LoginHint { get; set; } = string.Empty;
        [BindNever] public string Initials { get; set; } = "?";

        public bool AskUsername => Mode != "user";
    }

    public sealed record RecentAccountView(string Login, string Name, string RoleLabel, string LastAtText, string Initials);
}
