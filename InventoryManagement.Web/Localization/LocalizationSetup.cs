using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    /// <summary>Only the UI culture changes, formatting stays en-US so forms and JS keep a decimal point.</summary>
    public static class LocalizationSetup
    {
        public const string DefaultUiCulture = "az-Latn-AZ";
        public const string FormattingCulture = "en-US";
        public static readonly string[] UiCultures = [DefaultUiCulture, "en-US", "ru-RU"];

        // Language switch entries in display order
        public static readonly (string Culture, string Label)[] Languages = [(DefaultUiCulture, "AZ"), ("en-US", "EN"), ("ru-RU", "RU")];

        public static string CookieValue(string uiCulture)
            => CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(FormattingCulture, uiCulture));

        public static IServiceCollection AddUiLocalization(this IServiceCollection services)
        {
            services.AddSingleton<IStringLocalizerFactory, JsonStringLocalizerFactory>();
            services.AddLocalization();
            // PostConfigure so it runs after MVC's data-annotations provider has filled in the attributes it reads
            services.PostConfigure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            {
                options.ModelMetadataDetailsProviders.Add(new DefaultValidationMessages());
                LocalizeBindingMessages(options.ModelBindingMessageProvider);
            });
            return services;
        }

        /// <summary>Replaces the framework's English model-binding messages, read at binding time so they follow the request's language.</summary>
        private static void LocalizeBindingMessages(Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.DefaultModelBindingMessageProvider messages)
        {
            var localizer = new JsonStringLocalizer();
            string Required() => localizer["This field is required."];
            string Invalid(string? value) => string.IsNullOrEmpty(value) ? Required() : localizer["The value '{0}' is not valid.", value];

            messages.SetMissingBindRequiredValueAccessor(_ => Required());
            messages.SetMissingKeyOrValueAccessor(Required);
            messages.SetValueMustNotBeNullAccessor(_ => Required());
            messages.SetAttemptedValueIsInvalidAccessor((value, _) => Invalid(value));
            messages.SetNonPropertyAttemptedValueIsInvalidAccessor(Invalid);
            messages.SetValueIsInvalidAccessor(Invalid);
            messages.SetUnknownValueIsInvalidAccessor(_ => localizer["The value is not valid."]);
            messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => localizer["The value is not valid."]);
            messages.SetValueMustBeANumberAccessor(_ => localizer["The field must be a number."]);
            messages.SetNonPropertyValueMustBeANumberAccessor(() => localizer["The field must be a number."]);
        }

        public static IApplicationBuilder UseUiLocalization(this IApplicationBuilder app)
        {
            var options = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(FormattingCulture, DefaultUiCulture),
                SupportedCultures = [new CultureInfo(FormattingCulture)],
                SupportedUICultures = UiCultures.Select(c => new CultureInfo(c)).ToList(),
                // Cookie only, so the browser's Accept-Language never overrides the Azerbaijani default
                RequestCultureProviders = [new CookieRequestCultureProvider()]
            };
            return app.UseRequestLocalization(options);
        }
    }
}
