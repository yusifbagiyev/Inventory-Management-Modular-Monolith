using IdentityService.Application.DTOs;

namespace IdentityService.Application.Services
{
    public interface IAuthService
    {
        Task<TokenDto> LoginAsync(LoginDto dto);
        Task<TokenDto> RegisterAsync(RegisterDto dto);
        Task<TokenDto> RefreshTokenAsync(RefreshTokenDto dto);
        Task<bool> HasPermissionAsync(int userId, string permission);
        Task<UserDto> GetUserAsync(int userId);
    }
}