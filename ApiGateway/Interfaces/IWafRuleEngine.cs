using ApiGateway.Dto;

namespace ApiGateway.Interfaces
{
    public interface IWafRuleEngine
    {
        Task<WafValidationResult> ValidateRequestAsync(HttpContext context);
    }
}