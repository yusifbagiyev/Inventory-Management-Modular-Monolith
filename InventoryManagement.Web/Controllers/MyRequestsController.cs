using InventoryManagement.Web.Models.DTOs;
using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize]
    public class MyRequestsController : Controller
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<MyRequestsController> _logger;

        public MyRequestsController(
            IApprovalService approvalService,
            ILogger<MyRequestsController> logger)
        {
            _approvalService = approvalService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var requests = await _approvalService.GetMyRequestsAsync();
                var model = new MyRequestsViewModel
                {
                    Requests = requests.OrderByDescending(r => r.CreatedAt).ToList()
                };
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading my requests");
                return View(new MyRequestsViewModel());
            }
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _approvalService.CancelRequestAsync(id);
                TempData["Success"] = "Request cancelled successfully";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling request {RequestId}", id);
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Details(int id)
        {
            var request = await _approvalService.GetRequestDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }
            return PartialView("_RequestDetails", request);
        }
    }
}