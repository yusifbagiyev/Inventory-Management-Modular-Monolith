using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DepartmentsController : BaseController
    {
        private readonly IApiService _apiService;

        public DepartmentsController(IApiService apiService, ILogger<DepartmentsController> logger)
            :base(logger)
        {
            _apiService = apiService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var departments = await _apiService.GetAsync<List<DepartmentViewModel>>("api/departments");
                return View(departments ?? []);
            }
            catch (Exception ex)
            {
                return HandleException(ex,new List<DepartmentViewModel>());
            }
        }



        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var department = await _apiService.GetAsync<DepartmentViewModel>($"api/departments/{id}");
                if (department == null)
                    return NotFound();

                // Get products for this department
                var products = await _apiService.GetAsync<PagedResultDto<ProductViewModel>>(
                    $"api/products?pageSize=10000&pageNumber=1&departmentId={id}");

                var departmentProducts = products?.Items?? new List<ProductViewModel>();

                ViewBag.Products = departmentProducts;

                // Update counts
                department.ProductCount = departmentProducts.Count();
                department.WorkerCount = departmentProducts
                    .Where(w => !string.IsNullOrEmpty(w.Worker))
                    .Select(w => w.Worker)
                    .Distinct()
                    .Count();

                return View(department);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        public IActionResult Create()
        {
            return View(new DepartmentViewModel());
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DepartmentViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return HandleValidationErrors(ModelState);
            }
            try
            {
                var dto = new CreateDepartmentDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive
                };

                var response = await _apiService.PostAsync<CreateDepartmentDto, DepartmentDto>("api/departments", dto);
                return HandleApiResponse(response, "Index");
            }
            catch(Exception ex)
            {
                return HandleException(ex, model);
            }
        }



        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var department = await _apiService.GetAsync<DepartmentViewModel>($"api/departments/{id}");
                if (department == null)
                    return NotFound();

                return View(department);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DepartmentViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return HandleValidationErrors(ModelState);
            }
            try
            {
                var dto= new UpdateDepartmentDto
                {
                    Name = model.Name,
                    Description = model.Description,
                    IsActive = model.IsActive
                };
                var response = await _apiService.PutAsync<UpdateDepartmentDto, bool>($"api/departments/{id}", dto);
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex, model);
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var response = await _apiService.DeleteAsync($"api/departments/{id}");
                return HandleApiResponse(response, "Index");
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}