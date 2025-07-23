using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ApprovalsController : Controller
    {
        private readonly IApprovalService _approvalService;
        private readonly ILogger<ApprovalsController> _logger;

        public ApprovalsController(
            IApprovalService approvalService, ILogger<ApprovalsController> logger)
        {
            _approvalService = approvalService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var pendingRequests = await _approvalService.GetPendingRequestsAsync();
                var statistics = await _approvalService.GetStatisticsAsync();

                var model = new ApprovalDashboardViewModel
                {
                    PendingRequests = pendingRequests,
                    TotalPending = statistics.TotalPending,
                    TotalApproved = statistics.TotalApprovedToday,
                    TotalRejected = statistics.TotalRejectedToday
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading approval dashboard");
                var model = new ApprovalDashboardViewModel
                {
                    PendingRequests = new List<Models.DTOs.ApprovalRequestDto>()
                };
                return View(model);
            }
        }


        public async Task<IActionResult> Details(int id)
        {
            var request = await _approvalService.GetRequestDetailsAsync(id);
            if (request == null)
            {
                return NotFound();
            }
            return PartialView("_ApprovalDetails", request);
        }


        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                var approvalRequest = await _approvalService.GetRequestDetailsAsync(id);
                if (approvalRequest == null)
                    return NotFound();

                await _approvalService.ApproveRequestAsync(id);
                return Json(new { success = true, message = "Request approved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving request {RequestId}", id);
                return Json(new { success = false, message = "Failed to approve request: " + ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            try
            {
                await _approvalService.RejectRequestAsync(id, reason);
                return Json(new { success = true, message = "Request rejected" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting request {RequestId}", id);
                return Json(new { success = false, message = "Failed to reject request: " + ex.Message });
            }
        }
    }
}