using System.Net;
using System.Net.Http.Json;
using InventoryManagement.Tests.Infrastructure;

namespace InventoryManagement.Tests
{
    [Collection(nameof(AppCollection))]
    public class RouteFlowTests(AppFixture app)
    {
        private static MultipartFormDataContent TransferForm(int productId, int toDepartmentId, string toWorker) => new()
        {
            { new StringContent(productId.ToString()), "ProductId" },
            { new StringContent(toDepartmentId.ToString()), "ToDepartmentId" },
            { new StringContent(toWorker), "ToWorker" }
        };

        [Fact]
        public async Task Completing_a_transfer_moves_the_product_once()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var from = await app.Admin.CreateDepartmentAsync();
            var to = await app.Admin.CreateDepartmentAsync();
            var productId = await app.Admin.CreateProductAsync(category, from, worker: "Ali");

            var routeId = await CatalogBuilder.IdOf(await app.Admin.PostAsync("/api/inventoryroutes/transfer", TransferForm(productId, to, "Veli")));

            // Nothing moves until the route completes.
            Assert.Equal(from, await app.ScalarAsync<int>($"SELECT \"DepartmentId\" FROM product.\"Products\" WHERE \"Id\" = {productId}"));

            var complete = await app.Admin.PutAsync($"/api/inventoryroutes/{routeId}/complete", null);
            var again = await app.Admin.PutAsync($"/api/inventoryroutes/{routeId}/complete", null);

            Assert.Equal(HttpStatusCode.NoContent, complete.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
            Assert.Equal($"{to}|Veli",
                await app.ScalarAsync<string>($"SELECT \"DepartmentId\" || '|' || \"Worker\" FROM product.\"Products\" WHERE \"Id\" = {productId}"));
        }

        [Fact]
        public async Task A_product_cannot_have_two_pending_transfers()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var from = await app.Admin.CreateDepartmentAsync();
            var to = await app.Admin.CreateDepartmentAsync();
            var productId = await app.Admin.CreateProductAsync(category, from);

            var first = await app.Admin.PostAsync("/api/inventoryroutes/transfer", TransferForm(productId, to, "A"));
            var second = await app.Admin.PostAsync("/api/inventoryroutes/transfer", TransferForm(productId, to, "B"));

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task Approved_notes_only_route_update_keeps_the_worker()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var from = await app.Admin.CreateDepartmentAsync();
            var to = await app.Admin.CreateDepartmentAsync();
            var productId = await app.Admin.CreateProductAsync(category, from);
            var routeId = await CatalogBuilder.IdOf(await app.Admin.PostAsync("/api/inventoryroutes/transfer", TransferForm(productId, to, "Aysel")));

            var update = new MultipartFormDataContent
            {
                { new StringContent("new note"), "Notes" },
                { new StringContent("Aysel"), "ToWorker" }
            };
            var requestId = await CatalogBuilder.ApprovalIdOf(await app.Operator.PutAsync($"/api/inventoryroutes/{routeId}", update));
            await app.Admin.PostAsync($"/api/approvalrequests/{requestId}/approve", null);

            Assert.Equal("Aysel|new note",
                await app.ScalarAsync<string>($"SELECT \"ToWorker\" || '|' || \"Notes\" FROM route.\"InventoryRoutes\" WHERE \"Id\" = {routeId}"));
        }

        [Fact]
        public async Task Inventory_code_change_is_recorded_in_history()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var productId = await app.Admin.CreateProductAsync(category, department);
            var newCode = CatalogBuilder.NextInventoryCode();

            var response = await app.Admin.PutAsJsonAsync($"/api/products/{productId}/inventory-code", new { inventoryCode = newCode });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal(1L, await app.ScalarAsync<long>(
                $"SELECT count(*) FROM route.\"InventoryRoutes\" WHERE \"ProductId\" = {productId} AND \"RouteType\" = 'Update' AND \"InventoryCode\" = {newCode}"));
        }
    }
}
