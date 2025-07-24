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

    // Permette di ottenere il Ruolo dell'utente identificato dalla matricola fornita.
    public async Task<string?> UserRole(int matricola)
    {
        return await _context.Utenti
            .Where(u => u.Matricola == matricola)
            .Select(u => u.Ruolo)
            .FirstOrDefaultAsync();
    }
}
    