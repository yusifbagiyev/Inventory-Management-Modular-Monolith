using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InventoryManagement.Tests.Infrastructure
{
    /// <summary>Creates catalog data through the public API. Inventory codes are unique per call.</summary>
    public static class CatalogBuilder
    {
        private static int _nextCode = 1000;

        public static int NextInventoryCode() => Interlocked.Increment(ref _nextCode);

        public static async Task<int> CreateCategoryAsync(this HttpClient admin, string? name = null)
            => await IdOf(await admin.PostAsJsonAsync("/api/categories",
                new { name = name ?? "Category " + Guid.NewGuid().ToString("N")[..8], description = "", isActive = true }));

        public static async Task<int> CreateDepartmentAsync(this HttpClient admin, string? name = null)
            => await IdOf(await admin.PostAsJsonAsync("/api/departments",
                new { name = name ?? "Department " + Guid.NewGuid().ToString("N")[..8], departmentHead = "Head", description = "", isActive = true }));

        /// <summary>POST /api/products as multipart, like the UI and ServiceDesk do.</summary>
        public static Task<HttpResponseMessage> PostProductAsync(
            this HttpClient client, int inventoryCode, int categoryId, int departmentId, string model = "Model", string? worker = null)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(inventoryCode.ToString()), "InventoryCode" },
                { new StringContent(model), "Model" },
                { new StringContent("Vendor"), "Vendor" },
                { new StringContent(categoryId.ToString()), "CategoryId" },
                { new StringContent(departmentId.ToString()), "DepartmentId" },
                { new StringContent("true"), "IsWorking" },
                { new StringContent("true"), "IsActive" },
                { new StringContent("true"), "IsNewItem" }
            };
            if (worker != null)
                form.Add(new StringContent(worker), "Worker");
            return client.PostAsync("/api/products", form);
        }

        public static async Task<int> CreateProductAsync(this HttpClient admin, int categoryId, int departmentId, string? worker = null)
        {
            var response = await admin.PostProductAsync(NextInventoryCode(), categoryId, departmentId, worker: worker);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return await IdOf(response);
        }

        public static async Task<int> ApprovalIdOf(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("approvalRequestId").GetInt32();
        }

        public static async Task<int> IdOf(HttpResponseMessage response)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("id").GetInt32();
        }
    }
}
