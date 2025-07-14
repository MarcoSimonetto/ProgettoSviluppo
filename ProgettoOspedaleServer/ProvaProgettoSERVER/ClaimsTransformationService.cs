using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
namespace ProvaProgettoSERVER
{
    public class ClaimsTransformationService : IClaimsTransformation
    {
        private readonly UserService userService;

        public ClaimsTransformationService(UserService userService)
        {
            this.userService = userService;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true)
                return principal;

            var matricolaClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricolaInt))
            {
                // Se la conversione fallisce, restituisco comunque il principal
                return principal;
            }

            var roles = await userService.UserRoles(matricolaInt);

            if (roles.Count == 0)
                return principal;

            var identity = (ClaimsIdentity)principal.Identity;

            foreach (var role in roles)
            {
                if (!principal.HasClaim(ClaimTypes.Role, role))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            return principal;
        }

    }
}
