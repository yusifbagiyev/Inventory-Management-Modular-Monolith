using System.Text.Json;
using ApprovalService.Domain.Repositories;
using MediatR;
using SharedServices.Contracts;
using SharedServices.Enum;

namespace ApprovalService.Application.Features.Queries
{
    /// <summary>
    /// Products with a request waiting for approval (update, delete, transfer), mapped to that
    /// request's id: the product list marks them and the details page links to the request.
    /// Pending requests are few, so they are read once and matched in memory.
    /// </summary>
    public class GetPendingProductRequests
    {
        public record Query : IRequest<IReadOnlyDictionary<int, int>>;

        private const int MaxPending = 500;

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
                // Newest first: a product with several pending requests points at the latest.
                foreach (var pending in await _repository.GetPendingAsync(1, MaxPending, cancellationToken))
                {
                    var productId = pending.RequestType switch
                    {
                        RequestType.UpdateProduct or RequestType.DeleteProduct => pending.EntityId ?? ProductIdIn(pending.ActionData),
                        RequestType.TransferProduct => ProductIdIn(pending.ActionData),
                        _ => 0
                    };
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
