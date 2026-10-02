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
            services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
                options.ModelMetadataDetailsProviders.Add(new DefaultValidationMessages()));
            return services;
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
