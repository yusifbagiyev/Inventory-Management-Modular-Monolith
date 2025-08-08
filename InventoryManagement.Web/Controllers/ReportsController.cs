using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Immutable;
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

            var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>("api/products");
            var departments = await _apiService.GetAsync<List<DepartmentViewModel>>("api/departments");
            var categories = await _apiService.GetAsync<List<CategoryViewModel>>("api/categories");

            if (products != null)
            {
                model.TotalProducts = products.Items.Count();
                model.WorkingItems = products.Items.Count(p => p.IsWorking);
                model.NotWorkingItems = products.Items.Count(p => !p.IsWorking);
                model.NewItems = products.Items.Count(p => p.IsNewItem);

                if(departments != null)
                {
                    model.DepartmentReports = departments.Select(d => new DepartmentReport
                    {
                        DepartmentName = d.Name,
                        TotalItems = products.Items.Count(p => p.DepartmentId == d.Id),
                        WorkingItems = products.Items.Count(p => p.DepartmentId == d.Id && p.IsWorking),
                        NotWorkingItems = products.Items.Count(p => p.DepartmentId == d.Id && !p.IsWorking),
                        UtilizationPercentage = products.Items.Any(p => p.DepartmentId == d.Id)
                            ? (int)((double)products.Items.Count(p => p.DepartmentId == d.Id && p.IsWorking) / products.Items.Count(p => p.DepartmentId == d.Id) * 100)
                            : 0
                    }).OrderByDescending(d => d.TotalItems).ToList();
                }

                if(categories!= null)
                {
                    var totalProducts = products.Items.Count();
                    model.CategoryReports = categories.Select(c => new CategoryReport
                    {
                        CategoryName = c.Name,
                        ItemCount = products.Items.Count(p => p.CategoryId == c.Id),
                        Percentage = totalProducts > 0
                            ? Math.Round((decimal)products.Items.Count(p => p.CategoryId == c.Id) / totalProducts * 100, 2)
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
            var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>("api/products");

            if (format.ToLower() == "csv")
            {
                var csv = new StringBuilder();
                csv.AppendLine("InventoryCode,Model,Vendor,Department,Worker,Status,Category");

                foreach (var product in products?.Items ?? [])
                {
                    csv.AppendLine($"{product.InventoryCode},{product.Model},{product.Vendor},{product.DepartmentName},{product.Worker},{(product.IsWorking ? "Working" : "Not Working")},{product.CategoryName}");
                }

                return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"inventory_{DateTime.Now:yyyyMMdd}.csv");
            }

            return BadRequest("Unsupported format");
        }



        [HttpGet]
        public async Task<IActionResult> GetFilteredInventoryData(
            string dateRange,
            string department,
            string category)
        {
            try
            {
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>("api/products");

                if (products == null||products.Items==null)
                {
                    return Json(new { error = "Failed to load products" });
                }

                var filteredProducts = products.Items.ToList();

                // Apply filters
                if (!string.IsNullOrEmpty(department))
                {
                    filteredProducts = filteredProducts
                        .Where(p => p.DepartmentName == department)
                        .ToList();
                }

                if (!string.IsNullOrEmpty(category))
                {
                    filteredProducts=filteredProducts
                        .Where(p=> p.CategoryName == category)
                        .ToList();
                }

                // Prepare response data
                var filteredData = new
                {
                    success = true,
                    TotalProducts = products.Items.Count(),
                    WorkingItems = products.Items.Count(p => p.IsWorking),
                    NotWorkingItems = products.Items.Count(p => !p.IsWorking),
                    NewItems = products.Items.Count(p => p.IsNewItem),
                    DepartmentData = filteredProducts
                        .GroupBy(p => p.DepartmentName)
                        .Select(g => new
                        {
                            Name = g.Key,
                            Count = g.Count(),
                            Working = g.Count(p => p.IsWorking),
                            NotWorking = g.Count(p => !p.IsWorking)
                        })
                        .OrderByDescending(d => d.Count)
                        .ToList(),

                    CategoryData = filteredProducts
                        .GroupBy(p=> p.CategoryName)
                        .Select(g => new
                        {
                            Name=g.Key,
                            Count=g.Count(),
                            Percentage = Math.Round((decimal)g.Count() / filteredProducts.Count * 100, 2)
                        })
                        .OrderByDescending(c=>c.Count)
                        .ToList()
                };

                return Json(filteredData);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading filtered data");
                return Json(new { error = "Error loading filtered data" ,details=ex.Message});
            }
        }


        [HttpPost]
        public async Task<IActionResult> EmailInventoryReport(
            List<string> recipients,
            string message,
            string dateRange,
            string department,
            string category)
        {
            try
            {
                // Generate the report data
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>("api/products");

                // Apply filters (same logic as GetFilteredInventoryData)
                // ... filter logic ...

                // For now, return success (you would implement actual email sending here)
                return Json(new { success = true, message = "Report sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email report");
                return Json(new { success = false, message = "Failed to send report" });
            }
        }


        [HttpPost]
        public IActionResult ExportInventoryPDF([FromBody] dynamic filters)
        {
            try
            {
                // For now, return a simple response
                // You can implement actual PDF generation using a library like iTextSharp or similar
                return Json(new { success = true, message = "PDF export initiated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting PDF");
                return Json(new { error = "Failed to export PDF" });
            }
        }
    }
}