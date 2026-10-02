namespace RouteService.Domain.Common
{
    /// <summary>One transfer with just the fields the dashboard counts by.</summary>
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
