using System.Text.Json;
using SharedServices.DTOs;

namespace SharedServices.Contracts
{
    /// <summary>Lets a module park an action for admin approval.</summary>
    public interface IApprovalRequests
    {
        Task<int> SubmitAsync(
            CreateApprovalRequestDto request,
            int requestedById,
            string requestedByName,
            CancellationToken cancellationToken = default);
    }

    public record ApprovalActor(int UserId, string UserName);

    // Implementations throw on failure. The message is recorded on the request.
    /// <summary>Runs an approved request. Each owning module registers one for its request types.</summary>
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
