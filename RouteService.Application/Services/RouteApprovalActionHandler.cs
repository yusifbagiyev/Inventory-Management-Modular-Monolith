using System.Text.Json;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using RouteService.Application.Features.Routes.Queries;
using SharedServices.Contracts;
using SharedServices.Enum;
using SharedServices.Exceptions;

namespace RouteService.Application.Services
{
    /// <summary>Executes approved transfer / route update / route delete requests in-process.</summary>
    public class RouteApprovalActionHandler : IApprovalActionHandler
    {
        private readonly IMediator _mediator;

        public RouteApprovalActionHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public bool CanHandle(string requestType) =>
            requestType is RequestType.TransferProduct or RequestType.UpdateRoute or RequestType.DeleteRoute;

        public Task ExecuteAsync(string requestType, JsonElement actionData, ApprovalActor approver, CancellationToken cancellationToken) =>
            requestType switch
            {
                RequestType.TransferProduct => TransferAsync(actionData, cancellationToken),
                RequestType.UpdateRoute => UpdateAsync(actionData, cancellationToken),
                RequestType.DeleteRoute => _mediator.Send(new DeleteRoute.Command(RequireId(actionData, "routeId")), cancellationToken),
                _ => throw new NotSupportedException($"Request type '{requestType}' is not handled by the routes module")
            };

        private Task TransferAsync(JsonElement data, CancellationToken cancellationToken)
        {
            var dto = new TransferInventoryDto
            {
                ProductId = RequireId(data, "productId"),
                ToDepartmentId = RequireId(data, "toDepartmentId"),
                ToWorker = data.GetString("toWorker"),
                Notes = data.GetString("notes"),
                ImageFile = data.GetImage()
            };
            return _mediator.Send(new TransferInventory.Command(dto), cancellationToken);
        }

        private async Task UpdateAsync(JsonElement root, CancellationToken cancellationToken)
        {
            var routeId = RequireId(root, "routeId");
            var data = root.Section("UpdateData");

            var existing = await _mediator.Send(new GetRouteByIdQuery(routeId), cancellationToken)
                ?? throw new NotFoundException($"Route with ID {routeId} not found");

            // The request only carries the fields that changed; everything else keeps its current
            // value. The previous HTTP-based executor forwarded notes alone, which cleared the
            // worker and dropped approved destination/image changes.
            var dto = new UpdateRouteDto
            {
                Notes = data.Has("notes") ? data.GetString("notes") : existing.Notes,
                ToWorker = data.Has("toWorker") ? data.GetString("toWorker") : existing.ToWorker,
                ToDepartmentId = data.Has("toDepartmentId") ? data.GetInt("toDepartmentId") : null,
                ImageFile = data.GetImage()
            };
            await _mediator.Send(new UpdateRoute.Command(routeId, dto), cancellationToken);
        }

        private static int RequireId(JsonElement element, string name)
        {
            var id = element.GetInt(name);
            return id > 0 ? id : throw new InvalidOperationException($"'{name}' is missing from the request data");
        }
    }
}
