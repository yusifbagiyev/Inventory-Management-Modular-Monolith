using ApprovalService.Shared.DTOs;
using ProductService.Application.DTOs;

namespace ProductService.Application.Interfaces
{
    public interface IApprovalService
    {
        Task<ApprovalRequestDto> CreateApprovalRequestAsync(CreateApprovalRequestDto dto, int userId, string userName);
    }
}