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

        private const int DecidedPageSize = 50;

        /// <param name="status">The tab: pending (default), approved or rejected.</param>
        public async Task<IActionResult> Index(string? status = null)
        {
            var tab = status is "approved" or "rejected" ? status : "pending";
            var statistics = await _approvalService.GetStatisticsAsync();
            var decided = await _approvalService.GetDecidedRequestsAsync(
                tab == "pending" ? null : tab == "approved", DecidedPageSize);

            return View(new ApprovalDashboardViewModel
            {
                Tab = tab,
                Requests = tab == "pending" ? await _approvalService.GetPendingRequestsAsync() : decided.Items,
                TotalPending = statistics.TotalPending,
                TotalApproved = statistics.TotalApprovedToday,
                TotalRejected = statistics.TotalRejectedToday,
                ApprovedCount = decided.ApprovedCount,
                RejectedCount = decided.RejectedCount
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

            if (response.Data)
                return Json(new { success = true, message = Tr("Request approved successfully") });

            // Say why: the stored reason ("Execution error: Product with inventory code 1003 already exists").
            var failed = await _approvalService.GetRequestDetailsAsync(id);
            var reason = failed?.RejectionReason;
            return Json(new
            {
                success = false,
                message = string.IsNullOrWhiteSpace(reason)
                    ? Tr("The request was approved but its action failed to execute. The requester has been notified.")
                    : string.Format(Tr("The request could not be carried out: {0}. The requester has been notified."), Tr(reason).TrimEnd('.'))
            });
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
