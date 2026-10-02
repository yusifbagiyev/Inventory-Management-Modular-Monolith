using System.Text.Json;
using ApprovalService.Application.Interfaces;
using SharedServices.Contracts;

namespace ApprovalService.Application.Services
{
    /// <summary>Dispatches an approved request to the module that owns its request type.</summary>
    public class ActionExecutor : IActionExecutor
    {
        private readonly IEnumerable<IApprovalActionHandler> _handlers;

        public ActionExecutor(IEnumerable<IApprovalActionHandler> handlers)
        {
            _handlers = handlers;
        }

        public async Task ExecuteAsync(
            string requestType,
            string actionData,
            ApprovalActor approver,
            CancellationToken cancellationToken = default)
        {
            var handler = _handlers.FirstOrDefault(h => h.CanHandle(requestType))
                ?? throw new NotSupportedException($"Request type '{requestType}' is not supported");

            using var document = JsonDocument.Parse(actionData);
            await handler.ExecuteAsync(requestType, document.RootElement, approver, cancellationToken);
        }
    }
}
