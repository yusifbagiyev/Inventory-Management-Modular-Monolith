namespace RouteService.Domain.Common
{
    public record TransferActivity(
        int ProductId,
        int? FromDepartmentId,
        string? FromDepartmentName,
        int ToDepartmentId,
        string ToDepartmentName,
        string? FromWorker,
        string? ToWorker,
        string CategoryName,
        bool IsCompleted,
        DateTime CreatedAt);
}
