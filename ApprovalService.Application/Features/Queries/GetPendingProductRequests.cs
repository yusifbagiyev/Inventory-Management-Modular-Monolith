using System.Text.Json;
using ApprovalService.Domain.Repositories;
using MediatR;
using SharedServices.Contracts;
using SharedServices.Enum;

namespace ApprovalService.Application.Features.Queries
{
    /// <summary>Maps each product with a pending request to that request's id, for the product list and details.</summary>
    public class GetPendingProductRequests
    {
        public record Query : IRequest<IReadOnlyDictionary<int, int>>;

        // Pending requests are few, so they are read once and matched in memory
        private const int MaxPending = 500;

        private static readonly string[] ProductRequestTypes =
            [RequestType.UpdateProduct, RequestType.DeleteProduct, RequestType.TransferProduct];

        public class Handler : IRequestHandler<Query, IReadOnlyDictionary<int, int>>
        {
            private readonly IApprovalRequestRepository _repository;

            public Handler(IApprovalRequestRepository repository)
            {
                _repository = repository;
            }

            public async Task<IReadOnlyDictionary<int, int>> Handle(Query request, CancellationToken cancellationToken)
            {
                var result = new Dictionary<int, int>();
                // Newest first, so a product with several pending requests points at the latest
                foreach (var pending in await _repository.GetPendingAsync(ProductRequestTypes, MaxPending, cancellationToken))
                {
                    // A transfer's EntityId is empty because its route does not exist yet
                    var productId = pending.RequestType == RequestType.TransferProduct
                        ? ProductIdIn(pending.ActionData)
                        : pending.EntityId ?? ProductIdIn(pending.ActionData);
                    if (productId > 0)
                        result.TryAdd(productId, pending.Id);
                }
                return result;
            }

            private static int ProductIdIn(string actionData)
            {
                try
                {
                    using var document = JsonDocument.Parse(actionData);
                    return document.RootElement.GetInt("productId");
                }
                catch (JsonException)
                {
                    return 0;
                }
            }
        }
    }
}
