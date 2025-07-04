using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagement.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ApprovalsController : Controller
    {
        private readonly IApprovalService _approvalService;

        public ApprovalsController(IApprovalService approvalService)
        {
            _approvalService = approvalService;
        }

        public async Task<IActionResult> Index()
        {
            var model = new ApprovalDashboardViewModel
            {
                PendingRequests = await _approvalService.GetPendingRequestsAsync()
            };
            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var request = await _approvalService.GetRequestDetailsAsync(id);
            return PartialView("_ApprovalDetails", request);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            await _approvalService.ApproveRequestAsync(id);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            await _approvalService.RejectRequestAsync(id, reason);
            return Ok();
        }
    }
}