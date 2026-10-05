using AuditService.Queries;
using InventoryManagement.Web.Filters;
using MediatR;
using ProductService.Application.Features.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedServices.Identity;

namespace InventoryManagement.Web.Controllers
{
    /// <summary>Audit log of every change and sign-in, grouped by request.</summary>
    [Authorize]
    [PermissionAuthorize(AllPermissions.AuditView)]
    public class AuditController : BaseController
    {
        private readonly IMediator _mediator;

        public AuditController(IMediator mediator, ILogger<AuditController> logger) : base(logger)
        {
            _mediator = mediator;
        }

        // The arrays take the column headers' several choices and the filter bar's single one alike
        public async Task<IActionResult> Index(
            int pageNumber = 1, int pageSize = 30, string? search = null, int[]? userId = null,
            string[]? entityType = null, string[]? operation = null, DateTime? startDate = null, DateTime? endDate = null,
            string? entityId = null, string? sort = null, string? dir = null)
        {
            // Newest first unless a header asks otherwise
            var descending = sort == null || dir == "desc";
            var page = await _mediator.Send(new GetAuditLogQuery(
                pageNumber, pageSize, search, userId, entityType, operation,
                startDate?.Date, endDate?.Date.AddDays(1).AddTicks(-1), entityId, sort, descending));
            ViewBag.Facets = await _mediator.Send(new GetAuditFacetsQuery());
            // Changes store department and category ids, so the view needs the names
            var lookups = await _mediator.Send(new GetLookupsQuery());
            ViewBag.DepartmentNames = lookups.Departments.ToDictionary(d => d.Id, d => d.Name);
            ViewBag.CategoryNames = lookups.Categories.ToDictionary(c => c.Id, c => c.Name);
            return View(page);
        }
    }
}
