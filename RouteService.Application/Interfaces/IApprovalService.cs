using RouteService.Application.DTOs;
using SharedServices.DTOs;

namespace RouteService.Application.Interfaces
{
    public interface IApprovalService
    {
        Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName);
    }
}