using ApprovalService.Application.DTOs;
using ApprovalService.Domain.Entities;

namespace ApprovalService.Application.Mappings
{
    public static class ApprovalMappings
    {
        public static ApprovalRequestDto ToDto(this ApprovalRequest request) => new()
        {
            Id = request.Id,
            RequestType = request.RequestType,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            ActionData = request.ActionData,
            RequestedById = request.RequestedById,
            RequestedByName = request.RequestedByName,
            ApprovedById = request.ApprovedById,
            ApprovedByName = request.ApprovedByName,
            Status = request.Status.ToString(),
            RejectionReason = request.RejectionReason,
            CreatedAt = request.CreatedAt,
            ProcessedAt = request.ProcessedAt,
            ExecutedAt = request.ExecutedAt
        };

        public static ApprovalRequestDto ToDto(this ApprovalRequestSummary request) => new()
        {
            Id = request.Id,
            RequestType = request.RequestType,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            ActionData = request.ActionData,
            RequestedById = request.RequestedById,
            RequestedByName = request.RequestedByName,
            ApprovedById = request.ApprovedById,
            ApprovedByName = request.ApprovedByName,
            Status = request.Status.ToString(),
            RejectionReason = request.RejectionReason,
            CreatedAt = request.CreatedAt,
            ProcessedAt = request.ProcessedAt,
            ExecutedAt = request.ExecutedAt
        };
    }
}
