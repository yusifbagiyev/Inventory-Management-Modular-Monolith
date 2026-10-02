using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InventoryManagement.Web.Models.ViewModels
{
    /// <summary>Sign-in page that shows remembered accounts first, then asks for the password.</summary>
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

        /// <summary>With user the account was picked and the username stays hidden, anything else asks for both fields.</summary>
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
