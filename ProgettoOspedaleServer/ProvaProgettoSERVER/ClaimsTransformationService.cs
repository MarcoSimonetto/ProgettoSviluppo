using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
namespace ProvaProgettoSERVER
{
    // Questa classe serve per aggiungere dinamicamente i ruoli all'utente autenticato dopo che
    // l'autenticazione è già avvenuta.
    public class ClaimsTransformationService : IClaimsTransformation
    {
        private readonly UserService userService;

        public ClaimsTransformationService(UserService userService)
        {
            this.userService = userService;
        }

        // Viene verificato che l’utente sia autenticato
        // Estrae il valore della matricola (NameIdentifier) e la converte in intero.
        // Recupera il Ruolo dell'utente dal database richiamando il metodo all'interno di UserService.cs
        // passando la matricola.
        // Aggiunge il Ruolo ritornato dal database nel ClaimTypes.Role
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true)
                return principal;

            var matricolaClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricolaInt))
            {
                return principal;
            }

            var role = await userService.UserRole(matricolaInt);

            if (string.IsNullOrEmpty(role))
                return principal;

            var identity = (ClaimsIdentity)principal.Identity;

            if (!principal.HasClaim(ClaimTypes.Role, role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            return principal;
        }

    }
}
