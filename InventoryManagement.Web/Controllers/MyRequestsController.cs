using InventoryManagement.Web.Models.ViewModels;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedServices.Identity;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>A non-admin user's own approval requests.</summary>
    [Authorize]
    public class MyRequestsController : BaseController
    {
        private readonly IApprovalService _approvalService;

        public MyRequestsController(IApprovalService approvalService, ILogger<MyRequestsController> logger)
            : base(logger)
        {
            _approvalService = approvalService;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _approvalService.GetMyRequestsAsync();
            return View(new MyRequestsViewModel
            {
                Requests = requests,
                StatusCounts = requests.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count())
            });
        }

        public async Task<IActionResult> Details(int id)
        {
            // Null for a request owned by someone else
            var request = await _approvalService.GetRequestDetailsAsync(id);
            return request == null
                ? RedirectToNotFound()
                : PartialView("~/Views/Approvals/_ApprovalDetails.cshtml", request);
        }

        /// <summary>The module only lets the requester cancel, and only while the request is pending.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var response = await RunAsync(() => _approvalService.CancelRequestAsync(id), "Request cancelled successfully");
            return HandleApiResponse(response, nameof(Index));
        }
    }
}
