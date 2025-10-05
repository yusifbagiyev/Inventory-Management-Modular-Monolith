using ApiGateway.Dto;
using ApiGateway.Interfaces;

namespace ApiGateway.Security
{
    public class RequestSizeRule : IWafRule
    {
        private const long MaxRequestSize = 10 * 1024 * 1024; // 10MB

        public Task<WafValidationResult> ValidateAsync(HttpContext context)
        {
            if (context.Request.ContentLength > MaxRequestSize)
            {
                return Task.FromResult(new WafValidationResult
                {
                    IsValid = false,
                    Reason = $"Request size exceeds maximum allowed size of {MaxRequestSize} bytes",
                    BlockedBy = new List<string> { "RequestSizeRule" }
                });
            }

            return Task.FromResult(new WafValidationResult { IsValid = true });
        }
    }

}