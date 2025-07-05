namespace ApprovalService.Application.Interfaces
{
    public interface IActionExecutor
    {
        Task<bool> ExecuteAsync(string requestType, string actionData, CancellationToken cancellationToken = default);
    }
}