using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;

namespace InventoryManagement.Web.Filters
{
    /// <summary>Sends a sign-in or sign-out form whose token no longer fits the session to where the user belongs instead of an error page.</summary>
    /// <remarks>The token stops fitting when the user signed in or out in another tab after the page was opened.</remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SignInFormExpiredAttribute : Attribute, IAsyncAlwaysRunResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is IAntiforgeryValidationFailedResult)
                context.Result = await RedirectAsync(context);
            await next();
        }

        private static async Task<IActionResult> RedirectAsync(ResultExecutingContext context)
        {
            var request = context.HttpContext.Request;
            var form = request.HasFormContentType ? await request.ReadFormAsync() : null;
            string? Value(string name) => form?[name].FirstOrDefault() is { Length: > 0 } v ? v : request.Query[name].FirstOrDefault();

            var returnUrl = Value("ReturnUrl");
            if (returnUrl != null && !context.HttpContext.RequestServices.GetRequiredService<IUrlHelperFactory>().GetUrlHelper(context).IsLocalUrl(returnUrl))
                returnUrl = null;

            if (context.HttpContext.User.Identity?.IsAuthenticated == true)
                return returnUrl != null ? new LocalRedirectResult(returnUrl) : new RedirectToActionResult("Index", "Home", null);

            // A fresh sign-in page for the same account, so only the password has to be entered again
            var user = Value("Mode") == "user" ? Value("Username") : null;
            return new RedirectToActionResult("Login", "Account", new { returnUrl, user });
        }
    }
}
