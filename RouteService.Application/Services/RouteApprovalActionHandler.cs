using System.Text.Json;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Features.Routes.Commands;
using SharedServices.Contracts;
using SharedServices.Enum;

namespace RouteService.Application.Services
{
    /// <summary>Executes approved transfer, route update and route delete requests in-process.</summary>
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
                ImageFile = data.GetImage(),
                ImageFiles = data.GetImages()
            };
            return _mediator.Send(new TransferInventory.Command(dto), cancellationToken);
        }

        private Task UpdateAsync(JsonElement root, CancellationToken cancellationToken)
        {
            var routeId = RequireId(root, "routeId");
            var data = root.Section("UpdateData");

            // The request carries only the changed fields, and a missing one keeps the value the route has by now
            var dto = new UpdateRouteDto
            {
                Notes = data.Has("notes") ? data.GetString("notes") : null,
                ToWorker = data.Has("toWorker") ? data.GetString("toWorker") : null,
                ToDepartmentId = data.Has("toDepartmentId") ? data.GetInt("toDepartmentId") : null,
                // Older requests carry a single image that replaces all images
                ImageFile = data.GetImage() ?? data.GetImages("replaceImages").FirstOrDefault(),
                ImageFiles = data.GetImages(),
                RemoveImageUrls = data.GetStrings("removeImageUrls"),
                CoverImageUrl = data.Has("coverImageUrl") ? data.GetString("coverImageUrl") : null
            };
            return _mediator.Send(new UpdateRoute.Command(routeId, dto), cancellationToken);
        }

        private static int RequireId(JsonElement element, string name)
        {
            var id = element.GetInt(name);
            return id > 0 ? id : throw new InvalidOperationException($"'{name}' is missing from the request data");
        }
    }
}
