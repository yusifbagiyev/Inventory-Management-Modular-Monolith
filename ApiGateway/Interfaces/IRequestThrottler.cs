using ApiGateway.Dto;

namespace ApiGateway.Interfaces
{
    public interface IRequestThrottler
    {
        Task<ThrottleResult> ShouldThrottleAsync(HttpContext context);
    }
}