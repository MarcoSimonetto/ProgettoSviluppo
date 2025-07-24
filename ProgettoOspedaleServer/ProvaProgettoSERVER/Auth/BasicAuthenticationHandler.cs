using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ProvaProgettoSERVER.Services;
using Microsoft.EntityFrameworkCore;

namespace ProvaProgettoSERVER.Auth;

// Intercetta ogni richiesta HTTP, legge l’header Authorization, decodifica le credenziali (nel nostro caso Matricola e
// Password) e, se valide, crea un utente autenticato.

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly OspedaleContext _context;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TimeProvider clock,
        OspedaleContext context)
        : base(options, logger, encoder)
    {
        _context = context;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].ToString();
        Console.WriteLine("Authorization Header Received: " + authHeader);



        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        string authorizationHeader = Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        if (!authorizationHeader.StartsWith("basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        var token = authorizationHeader.Substring(6);
        string credentialAsString;
        try
        {
            credentialAsString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Base64 encoding");
        }

        var credentials = credentialAsString.Split(":");
        if (credentials.Length != 2)
        {
            return AuthenticateResult.Fail("Unauthorized");
        }

        var matricolaStr = credentials[0];
        var password = credentials[1];

        if (!int.TryParse(matricolaStr, out var matricolaInt))
        {
            return AuthenticateResult.Fail("La matricola contiene solo valori numerici!");
        }

        var utente = await _context.Utenti
            .SingleOrDefaultAsync(u => u.Matricola == matricolaInt && u.Password == password);

        if (utente == null)
        {
            return AuthenticateResult.Fail("Authentication failed");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, matricolaStr)
        };

        var identity = new ClaimsIdentity(claims, "Basic");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        return AuthenticateResult.Success(new AuthenticationTicket(claimsPrincipal, Scheme.Name));

    }
}
