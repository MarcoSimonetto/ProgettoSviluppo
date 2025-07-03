using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;

namespace ProvaProgettoSERVER.Services
{
    public class OspedaleContext : DbContext
    {
        public OspedaleContext(DbContextOptions<OspedaleContext> options) : base(options)
        {
        }
        public DbSet<Utente> Utenti { get; set; }
        public DbSet<Paziente> Pazienti { get; set; }
        public DbSet<Reparto> Reparti { get; set; }
        public DbSet<Terapia> Terapie { get; set; }
        public DbSet<Somministrazione> Somministrazioni { get; set; }
    }
}
