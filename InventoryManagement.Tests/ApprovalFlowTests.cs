using System.Net;
using InventoryManagement.Tests.Infrastructure;

namespace InventoryManagement.Tests
{
    [Collection(nameof(AppCollection))]
    public class ApprovalFlowTests(AppFixture app)
    {
        [Fact]
        public async Task Operator_create_is_queued_and_executes_on_approval()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var code = CatalogBuilder.NextInventoryCode();

            var requestId = await CatalogBuilder.ApprovalIdOf(await app.Operator.PostProductAsync(code, category, department));

            Assert.True(requestId > 0);
            Assert.Equal(0L, await app.ScalarAsync<long>($"SELECT count(*) FROM product.\"Products\" WHERE \"InventoryCode\" = {code}"));

            var approve = await app.Admin.PostAsync($"/api/approvalrequests/{requestId}/approve", null);

            Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
            Assert.Equal(1L, await app.ScalarAsync<long>($"SELECT count(*) FROM product.\"Products\" WHERE \"InventoryCode\" = {code}"));
            Assert.Equal("Executed", await app.ScalarAsync<string>($"SELECT \"Status\" FROM approval.\"ApprovalRequests\" WHERE \"Id\" = {requestId}"));
            // The creation's history row is written in the same transaction.
            Assert.Equal(1L, await app.ScalarAsync<long>($"SELECT count(*) FROM route.\"InventoryRoutes\" WHERE \"InventoryCode\" = {code} AND \"RouteType\" = 'New'"));
        }

        [Fact]
        public async Task Approving_twice_is_rejected()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var requestId = await CatalogBuilder.ApprovalIdOf(
                await app.Operator.PostProductAsync(CatalogBuilder.NextInventoryCode(), category, department));

            await app.Admin.PostAsync($"/api/approvalrequests/{requestId}/approve", null);
            var second = await app.Admin.PostAsync($"/api/approvalrequests/{requestId}/approve", null);

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task Failed_action_is_recorded_and_leaves_no_partial_data()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var code = CatalogBuilder.NextInventoryCode();

            var requestId = await CatalogBuilder.ApprovalIdOf(await app.Operator.PostProductAsync(code, category, department));
            // Someone creates the same inventory code directly before the request is approved.
            await app.Admin.CreateProductAsync(category, department); // unrelated product, same transaction path
            var direct = await app.Admin.PostProductAsync(code, category, department);
            Assert.Equal(HttpStatusCode.Created, direct.StatusCode);
            var routesBefore = await app.ScalarAsync<long>("SELECT count(*) FROM route.\"InventoryRoutes\"");

            var approve = await app.Admin.PostAsync($"/api/approvalrequests/{requestId}/approve", null);

            Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
            Assert.Equal("Failed", await app.ScalarAsync<string>($"SELECT \"Status\" FROM approval.\"ApprovalRequests\" WHERE \"Id\" = {requestId}"));
            Assert.Contains("already exists", await app.ScalarAsync<string>($"SELECT \"RejectionReason\" FROM approval.\"ApprovalRequests\" WHERE \"Id\" = {requestId}"));
            Assert.Equal(routesBefore, await app.ScalarAsync<long>("SELECT count(*) FROM route.\"InventoryRoutes\""));
            Assert.Equal(1L, await app.ScalarAsync<long>($"SELECT count(*) FROM product.\"Products\" WHERE \"InventoryCode\" = {code}"));
        }

        [Fact]
        public async Task Legacy_pending_request_in_old_ActionData_format_executes()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var productId = await app.Admin.CreateProductAsync(category, department, worker: "Old worker");

            // Shape written by the pre-monolith ProductManagementService: PascalCase root, nested UpdateData.
            var actionData = $$"""
                {"ProductId":{{productId}},"InventoryCode":1,"UpdateData":{"model":"Legacy model","vendor":"Vendor","worker":"New worker","description":"","categoryId":{{category}},"departmentId":{{department}},"isWorking":true,"isActive":true,"isNewItem":false,"imageUrl":""},"Changes":["Model"]}
                """;
            var requestId = await app.ScalarAsync<int>($"""
                INSERT INTO approval."ApprovalRequests" ("RequestType","EntityType","EntityId","ActionData","RequestedById","RequestedByName","Status","CreatedAt")
                VALUES ('product.update','Product',{productId},'{actionData}',1,'legacy','Pending',now()) RETURNING "Id"
                """);

            var approve = await app.Admin.PostAsync($"/api/approvalrequests/{requestId}/approve", null);

            Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
            Assert.Equal("Legacy model|New worker",
                await app.ScalarAsync<string>($"SELECT \"Model\" || '|' || \"Worker\" FROM product.\"Products\" WHERE \"Id\" = {productId}"));
        }

        [Fact]
        public async Task Only_the_requester_can_cancel()
        {
            var category = await app.Admin.CreateCategoryAsync();
            var department = await app.Admin.CreateDepartmentAsync();
            var requestId = await CatalogBuilder.ApprovalIdOf(
                await app.Operator.PostProductAsync(CatalogBuilder.NextInventoryCode(), category, department));

            var byAdmin = await app.Admin.DeleteAsync($"/api/approvalrequests/{requestId}/cancel");
            var byOwner = await app.Operator.DeleteAsync($"/api/approvalrequests/{requestId}/cancel");

            Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);
        }
    }
}
