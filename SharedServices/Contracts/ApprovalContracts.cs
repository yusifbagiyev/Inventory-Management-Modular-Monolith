using System.Text.Json;
using SharedServices.DTOs;

namespace SharedServices.Contracts
{
    /// <summary>Lets a module park an action for admin approval.</summary>
    public interface IApprovalRequests
    {
        /// <returns>The id of the new approval request.</returns>
        Task<int> SubmitAsync(
            CreateApprovalRequestDto request,
            int requestedById,
            string requestedByName,
            CancellationToken cancellationToken = default);
    }

    public record ApprovalActor(int UserId, string UserName);

    /// <summary>
    /// Executes an approved request. Each owning module registers one for the
    /// <see cref="Enum.RequestType"/> values it understands. Implementations throw on failure;
    /// the message is recorded on the request.
    /// </summary>
    public interface IApprovalActionHandler
    {
        bool CanHandle(string requestType);

        Task ExecuteAsync(
            string requestType,
            JsonElement actionData,
            ApprovalActor approver,
            CancellationToken cancellationToken);
    }
}
