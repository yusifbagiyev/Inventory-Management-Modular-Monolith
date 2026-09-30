using System.Net.Http.Headers;
using System.Net.Http.Json;
using IdentityService.Application.DTOs;
using IdentityService.Application.Services;
using InventoryManagement.Web.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedServices.Identity;
using Testcontainers.PostgreSql;

namespace InventoryManagement.Tests.Infrastructure
{
    /// <summary>
    /// The whole application against a throwaway PostgreSQL container (Docker required).
    /// Shared by every test in <see cref="AppCollection"/>; tests create their own data.
    /// </summary>
    public sealed class AppFixture : IAsyncLifetime
    {
        public const string Password = "Integration1Test";

        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:15-alpine").Build();

        private readonly string _imagesRoot = Path.Combine(Path.GetTempPath(), "inventory-tests-" + Guid.NewGuid().ToString("N"));

        public WebApplicationFactory<Program> Factory { get; private set; } = null!;

        /// <summary>Admin: every permission, approves requests.</summary>
        public HttpClient Admin { get; private set; } = null!;

        /// <summary>Operator: request (non-direct) permissions only, so writes go through approval.</summary>
        public HttpClient Operator { get; private set; } = null!;

        public string ConnectionString => _postgres.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();

            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
                builder.UseSetting("Jwt:Key", "integration-tests-signing-key-0123456789abcdef");
                builder.UseSetting("Jwt:Issuer", "166Logistics");
                builder.UseSetting("Jwt:Audience", "InventorySystemUsers");
                builder.UseSetting("WhatsApp:Enabled", "false");
                builder.UseSetting("ImageSettings:RootPath", _imagesRoot);
            });

            await Factory.Services.MigrateModulesAsync();

            await RegisterAsync("it-admin", AllRoles.Admin);
            await RegisterAsync("it-operator", AllRoles.Operator);
            // Operators get product create/update/delete as approval requests (see the Operator seed).
            Admin = await LoginAsync("it-admin");
            Operator = await LoginAsync("it-operator");
        }

        public HttpClient CreateClient(bool followRedirects = true)
            => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = followRedirects });

        public async Task<T> ScalarAsync<T>(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            return (T)(await command.ExecuteScalarAsync())!;
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private async Task RegisterAsync(string username, string role)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await auth.RegisterAsync(new RegisterDto
            {
                Username = username,
                Email = $"{username}@example.test",
                Password = Password,
                FirstName = "Integration",
                LastName = role,
                SelectedRole = role
            });
        }

        private async Task<HttpClient> LoginAsync(string username)
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadFromJsonAsync<TokenDto>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
            return client;
        }

        public async Task DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _postgres.DisposeAsync();
            if (Directory.Exists(_imagesRoot))
                Directory.Delete(_imagesRoot, recursive: true);
        }
    }

    [CollectionDefinition(nameof(AppCollection))]
    public sealed class AppCollection : ICollectionFixture<AppFixture>;
}
