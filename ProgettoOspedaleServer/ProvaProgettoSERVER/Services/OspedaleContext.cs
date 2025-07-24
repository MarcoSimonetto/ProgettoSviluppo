using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;

namespace ProvaProgettoSERVER.Services
{

    // Definisce il contesto del database usando EF Core.
    // OspedaleContext eredita da DbContext la classe base per interagire con un database.
    // Il costruttore riceve le opzioni (DbContextOptions) necessarie per configurare il contesto.
    // Definisco le entità come DbSet che rappresentano le tabelle del database.
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
