using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Web.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(SharedServices.Identity.PasswordRules.MaxUsernameLength)]
        [Display(Name = "Username")]
        public string Username { get; set; }=string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(SharedServices.Identity.PasswordRules.MaxPasswordLength)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }=string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}