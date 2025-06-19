using System.Security.Claims;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Services
{
    public interface ITokenService
    {
        Task<string> GenerateAccessToken(User user);
        Task<string> GenerateRefreshToken();
        ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}
