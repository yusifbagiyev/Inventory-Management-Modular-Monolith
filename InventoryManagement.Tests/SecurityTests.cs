using System.Net;
using System.Text.RegularExpressions;
using InventoryManagement.Tests.Infrastructure;

namespace InventoryManagement.Tests
{
    [Collection(nameof(AppCollection))]
    public partial class SecurityTests(AppFixture app)
    {
        [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
        private static partial Regex AntiforgeryField();

        /// <summary>Signs in through the login form, like a browser; the client keeps the cookie.</summary>
        private async Task<(HttpClient Client, string Token)> SignInWithCookieAsync()
        {
            var client = app.CreateClient(followRedirects: false);
            var loginPage = await client.GetStringAsync("/Account/Login");
            var token = AntiforgeryField().Match(loginPage).Groups[1].Value;

            var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Username"] = "it-admin",
                ["Password"] = AppFixture.Password,
                ["__RequestVerificationToken"] = token
            }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

            var page = await client.GetStringAsync("/Products");
            return (client, AntiforgeryField().Match(page).Groups[1].Value);
        }

        [Fact]
        public async Task Anonymous_api_call_gets_401_not_a_login_redirect()
        {
            var response = await app.CreateClient(followRedirects: false).GetAsync("/api/products");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Cookie_authenticated_api_writes_require_the_antiforgery_token()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var productId = await app.Admin.CreateProductAsync(category, department);
            var (client, token) = await SignInWithCookieAsync();

            var read = await client.GetAsync($"/api/products/{productId}");
            var forged = await client.DeleteAsync($"/api/products/{productId}");
            var withToken = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{productId}");
            withToken.Headers.Add("RequestVerificationToken", token);
            var legitimate = await client.SendAsync(withToken);

            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, legitimate.StatusCode);
        }

        [Fact]
        public async Task Ui_forms_post_to_ui_actions_not_to_the_api()
        {
            var (client, _) = await SignInWithCookieAsync();

            var createPage = await client.GetStringAsync("/Products/Create");

            Assert.Contains("action=\"/Products/Create\"", createPage);
            Assert.DoesNotContain("action=\"/api/", createPage);
        }

        [Fact]
        public async Task Removed_user_dump_endpoint_is_gone()
        {
            var response = await app.Operator.GetAsync("/api/auth/users/by-role/Admin");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
