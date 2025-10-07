using System.Security.Claims;

namespace PIOGHOASIS.Utils
{
    public static class ClaimsExtensions
    {
        private const string ModuleClaimType = "perm.module";
        public static bool HasModule(this ClaimsPrincipal user, string moduleCode)
            => user?.HasClaim(ModuleClaimType, moduleCode) == true;
    }
}
