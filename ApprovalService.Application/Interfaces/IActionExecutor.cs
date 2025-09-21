namespace ApprovalService.Application.Interfaces
{
    public interface IActionExecutor
    {
        Task<bool> ExecuteAsync(
            string requestType, 
            string actionData, 
            int userId,
            string userName,
            CancellationToken cancellationToken = default);
    }
}