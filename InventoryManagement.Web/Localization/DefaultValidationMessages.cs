using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace InventoryManagement.Web.Localization
{
    /// <summary>
    /// Data-annotations localization only translates attributes that carry an ErrorMessage; a bare
    /// [Required] falls back to the framework's built-in English text. This gives those attributes
    /// the framework's own wording as ErrorMessage, which the JSON localizer then translates.
    /// </summary>
    public sealed class DefaultValidationMessages : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (attribute.ErrorMessage != null || attribute.ErrorMessageResourceName != null)
                    continue;

                attribute.ErrorMessage = attribute switch
                {
                    RequiredAttribute => "The {0} field is required.",
                    StringLengthAttribute { MinimumLength: > 0 } => "The field {0} must be a string with a minimum length of {2} and a maximum length of {1}.",
                    StringLengthAttribute => "The field {0} must be a string with a maximum length of {1}.",
                    MaxLengthAttribute => "The field {0} must be a string or array type with a maximum length of '{1}'.",
                    RangeAttribute => "The field {0} must be between {1} and {2}.",
                    EmailAddressAttribute => "The {0} field is not a valid e-mail address.",
                    CompareAttribute => "'{0}' and '{1}' do not match.",
                    _ => null
                };
            }
        }
    }
}
