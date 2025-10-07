using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PIOGHOASIS.Infraestructure.Security
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class RequireModuleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _code;
        private const string ModuleClaimType = "perm.module";

        public RequireModuleAttribute(string code) => _code = code;

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new ChallengeResult(); // fuerza login
                return Task.CompletedTask;
            }

            if (!user.HasClaim(ModuleClaimType, _code))
            {
                // 403
                context.Result = new ForbidResult();
            }

            return Task.CompletedTask;
        }
    }
}
