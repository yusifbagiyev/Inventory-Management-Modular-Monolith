using SharedServices.Contracts;

namespace ApprovalService.Application.Interfaces
{
    public interface IActionExecutor
    {
        /// <summary>Runs the approved action. Throws when it cannot be executed.</summary>
        Task ExecuteAsync(
            string requestType,
            string actionData,
            ApprovalActor approver,
            CancellationToken cancellationToken = default);
    }
}
