using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedServices.Identity;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = AllRoles.Admin)]
    public class ApprovalsController : BaseController
    {
        private readonly IApprovalService _approvalService;

        public ApprovalsController(IApprovalService approvalService, ILogger<ApprovalsController> logger)
            : base(logger)
        {
            _approvalService = approvalService;
        }

        public async Task<IActionResult> Index()
        {
            var pendingRequests = await _approvalService.GetPendingRequestsAsync();
            var statistics = await _approvalService.GetStatisticsAsync();

            return View(new ApprovalDashboardViewModel
            {
                PendingRequests = pendingRequests,
                TotalPending = statistics.TotalPending,
                TotalApproved = statistics.TotalApprovedToday,
                TotalRejected = statistics.TotalRejectedToday
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            var request = await _approvalService.GetRequestDetailsAsync(id);
            return request == null ? RedirectToNotFound() : PartialView("_ApprovalDetails", request);
        }

        /// <summary>Approves and executes the request. A failed execution is reported, not thrown.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var response = await RunAsync(() => _approvalService.ApproveRequestAsync(id));
            if (!response.IsSuccess)
                return BadRequest(new { success = false, message = response.Message });

            return response.Data
                ? Json(new { success = true, message = Tr("Request approved successfully") })
                : Json(new { success = false, message = Tr("The request was approved but its action failed to execute. The requester has been notified.") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return HandleError("Rejection reason is required", null,
                    new Dictionary<string, string> { ["reason"] = "Please provide a reason for rejection" });
            }

            var response = await RunAsync(() => _approvalService.RejectRequestAsync(id, reason));
            return response.IsSuccess
                ? Json(new { success = true, message = Tr("Request rejected successfully") })
                : BadRequest(new { success = false, message = response.Message });
        }
    }
}
