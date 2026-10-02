using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SharedServices.Authorization
{
    // A name with a dot is a permission, and a|b means any of them. Other names go to the default provider.
    /// <summary>Builds permission policies on the fly so they need not be registered one by one.</summary>
    public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallback = new DefaultAuthorizationPolicyProvider(options);
        }

        public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            var policy = await _fallback.GetPolicyAsync(policyName);
            if (policy != null || !policyName.Contains('.'))
                return policy;

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
    }
}
