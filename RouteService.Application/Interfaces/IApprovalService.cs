using ApprovalService.Shared.DTOs;
using RouteService.Application.DTOs;

namespace RouteService.Application.Interfaces
{
    public interface IApprovalService
    {
        Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName);
    }
}