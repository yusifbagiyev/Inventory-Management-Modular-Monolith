using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace SharedServices.Authorization
{
    /// <summary>
    /// Resolves any policy name that looks like a permission ("product.view", "route.create.direct",
    /// or several joined with "|", meaning any of them) to a <see cref="PermissionRequirement"/>, so
    /// permissions do not have to be registered one by one. Everything else falls through to the
    /// default provider.
    /// </summary>
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
