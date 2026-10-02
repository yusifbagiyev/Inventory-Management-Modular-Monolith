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

    /// <summary>Runs an approved request for the owning module and throws on failure, so the message is recorded.</summary>
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
