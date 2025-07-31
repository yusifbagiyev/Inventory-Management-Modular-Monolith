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

            model.TotalProducts = products.Count;
            model.WorkingItems = products.Count(p => p.IsWorking);
            model.NotWorkingItems = products.Count(p => !p.IsWorking);
            model.NewItems = products.Count(p => p.IsNewItem);

            model.DepartmentReports = departments.Select(d => new DepartmentReport
            {
                DepartmentName = d.Name,
                TotalItems = products.Count(p => p.DepartmentId == d.Id),
                WorkingItems = products.Count(p => p.DepartmentId == d.Id && p.IsWorking),
                NotWorkingItems = products.Count(p => p.DepartmentId == d.Id && !p.IsWorking),
                UtilizationPercentage = products.Any(p => p.DepartmentId == d.Id)
                    ? (int)((double)products.Count(p => p.DepartmentId == d.Id && p.IsWorking) / products.Count(p => p.DepartmentId == d.Id) * 100)
                    : 0
            }).ToList();

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
                Routes = routes.Items.ToList(),
                StartDate = startDate,
                EndDate = endDate,
                DepartmentId = departmentId
            };

            return View(model);
        }
    }
}
