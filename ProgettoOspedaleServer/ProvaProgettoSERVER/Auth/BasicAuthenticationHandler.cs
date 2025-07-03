using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ProvaProgettoSERVER.Services; // Per il DbContext
using Microsoft.EntityFrameworkCore;

namespace ProvaProgettoSERVER.Auth;

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
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Missing Authorization Header");
        }

        string authorizationHeader = Request.Headers["Authorization"];
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return AuthenticateResult.Fail("Empty Authorization Header");
        }

        if (!authorizationHeader.StartsWith("basic ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Invalid Authorization Scheme");
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
            return AuthenticateResult.Fail("Invalid Basic Authentication Format");
        }

        var matricolaStr = credentials[0];
        var password = credentials[1];

        if (!int.TryParse(matricolaStr, out var matricolaInt))
        {
            return AuthenticateResult.Fail("Matricola must be numeric");
        }

        // 🔐 Verifica credenziali nel database
        var utente = await _context.Utenti
            .SingleOrDefaultAsync(u => u.Matricola == matricolaInt && u.Password == password);

        if (utente == null)
        {
            return AuthenticateResult.Fail("Invalid credentials");
        }

        // ✅ Crea i Claims dell’utente (matricola come stringa)
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, utente.Matricola.ToString()),
            new Claim(ClaimTypes.Name, utente.Matricola.ToString())
            // ⚠️ Il ruolo viene aggiunto dopo tramite ClaimsTransformationService
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
