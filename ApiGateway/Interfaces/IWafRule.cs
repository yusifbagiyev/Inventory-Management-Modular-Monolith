using ApiGateway.Dto;

namespace ApiGateway.Interfaces
{
    public interface IWafRule
    {
        Task<WafValidationResult> ValidateAsync(HttpContext context);
    }
}