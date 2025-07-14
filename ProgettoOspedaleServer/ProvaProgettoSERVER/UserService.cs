using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Services;

namespace ProvaProgettoSERVER;

public class UserService
{
    private readonly OspedaleContext _context;

    public UserService(OspedaleContext context)
    {
        _context = context;
    }

    public async Task<List<string>> UserRoles(int matricola)
    {
        var ruolo = await _context.Utenti
            .Where(u => u.Matricola == matricola)
            .Select(u => u.Ruolo)
            .FirstOrDefaultAsync();

        return string.IsNullOrEmpty(ruolo) ? new List<string>() : new List<string> { ruolo };
    }
}
    