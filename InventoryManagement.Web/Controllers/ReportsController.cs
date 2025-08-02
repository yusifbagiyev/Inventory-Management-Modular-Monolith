using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class ReportsController : BaseController
    {
        private readonly IApiService _apiService;

        public ReportsController(IApiService apiService, ILogger<ReportsController> logger)
            : base(logger)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Inventory()
        {
            var model = new InventoryReportViewModel();

            var products = await _apiService.GetAsync<List<ProductViewModel>>("api/products");
            var departments = await _apiService.GetAsync<List<DepartmentViewModel>>("api/departments");
            var categories = await _apiService.GetAsync<List<CategoryViewModel>>("api/categories");

            if (products != null)
            {
                model.TotalProducts = products.Count;
                model.WorkingItems = products.Count(p => p.IsWorking);
                model.NotWorkingItems = products.Count(p => !p.IsWorking);
                model.NewItems = products.Count(p => p.IsNewItem);

                if(departments != null)
                {
                    model.DepartmentReports = departments.Select(d => new DepartmentReport
                    {
                        DepartmentName = d.Name,
                        TotalItems = products.Count(p => p.DepartmentId == d.Id),
                        WorkingItems = products.Count(p => p.DepartmentId == d.Id && p.IsWorking),
                        NotWorkingItems = products.Count(p => p.DepartmentId == d.Id && !p.IsWorking),
                        UtilizationPercentage = products.Any(p => p.DepartmentId == d.Id)
                            ? (int)((double)products.Count(p => p.DepartmentId == d.Id && p.IsWorking) / products.Count(p => p.DepartmentId == d.Id) * 100)
                            : 0
                    }).OrderByDescending(d => d.TotalItems).ToList();
                }

                if(categories!= null)
                {
                    var totalProducts = products.Count;
                    model.CategoryReports = categories.Select(c => new CategoryReport
                    {
                        CategoryName = c.Name,
                        ItemCount = products.Count(p => p.CategoryId == c.Id),
                        Percentage = totalProducts > 0
                            ? Math.Round((decimal)products.Count(p => p.CategoryId == c.Id) / totalProducts * 100, 2)
                            : 0
                    }).Where(c => c.ItemCount > 0).OrderByDescending(c => c.ItemCount).ToList();
                }
            }
            return View(model);
        }

        public async Task<IActionResult> TransferHistory(DateTime? startDate, DateTime? endDate, int? departmentId)
        {
            var query = new StringBuilder("api/inventoryroutes?pageSize=1000");

            if (startDate.HasValue)
                query.Append($"&startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                query.Append($"&endDate={endDate.Value:yyyy-MM-dd}");

            var routes = await _apiService.GetAsync<PagedResultDto<RouteViewModel>>(query.ToString());

            var model = new TransferHistoryViewModel
            {
                Routes = routes?.Items.ToList() ?? [],
                StartDate = startDate,
                EndDate = endDate,
                DepartmentId = departmentId
            };

            if (routes != null && routes.Items.Any())
            {
                model.TotalTransfers = routes.Items.Count();
                model.CompletedTransfers = routes.Items.Count(r=>r.IsCompleted);
                model.PendingTransfers = routes.Items.Count(r => !r.IsCompleted);

                // Group by department
                model.TransfersByDepartment=routes.Items
                    .GroupBy(r=>r.ToDepartmentName)
                    .ToDictionary(g=>g.Key,g=>g.Count());

                // Group by month
                model.TransfersByMonth = routes.Items
                    .GroupBy(r => r.CreatedAt.ToString("yyyy-MM"))
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.Count());
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportInventory(string format = "csv")
        {
            var products = await _apiService.GetAsync<List<ProductViewModel>>("api/products");

            if (format.ToLower() == "csv")
            {
                var csv = new StringBuilder();
                csv.AppendLine("InventoryCode,Model,Vendor,Department,Worker,Status,Category");

                foreach (var product in products ?? [])
                {
                    csv.AppendLine($"{product.InventoryCode},{product.Model},{product.Vendor},{product.DepartmentName},{product.Worker},{(product.IsWorking ? "Working" : "Not Working")},{product.CategoryName}");
                }

                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"inventory_{DateTime.Now:yyyyMMdd}.csv");
            }

            return BadRequest("Unsupported format");
        }
    }
}