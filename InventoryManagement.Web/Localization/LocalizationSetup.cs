using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Localization;

namespace InventoryManagement.Web.Localization
{
    /// <summary>
    /// Interface language: Azerbaijani by default, English on request (cookie). Only the UI culture
    /// changes: the formatting culture stays en-US, so numbers keep a "." decimal separator in forms,
    /// model binding and JS; dates are always formatted explicitly (dd.MM.yyyy).
    /// </summary>
    public static class LocalizationSetup
    {
        public const string DefaultUiCulture = "az-Latn-AZ";
        public const string FormattingCulture = "en-US";
        public static readonly string[] UiCultures = [DefaultUiCulture, "en-US", "ru-RU"];

        /// <summary>The language switch: each culture and its short label, in display order.</summary>
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
                // The cookie only; the browser's Accept-Language must not override the Azerbaijani default.
                RequestCultureProviders = [new CookieRequestCultureProvider()]
            };
            return app.UseRequestLocalization(options);
        }
    }
}
