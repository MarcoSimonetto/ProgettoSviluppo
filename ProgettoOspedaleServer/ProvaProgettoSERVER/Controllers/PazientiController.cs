using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace ProvaProgettoSERVER.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PazientiController : ControllerBase
{
    private readonly OspedaleContext _context;

    public PazientiController(OspedaleContext context)
    {
        _context = context;
    }

    /*private async Task<Paziente?> TrovaPaziente(string CF)
    {
        return await _context.Pazienti.FindAsync(CF);
    }*/

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var pazienti = await _context.Pazienti.ToListAsync();
            return Ok(pazienti);
        }
        catch (SqlException ex)
        {
            return BadRequest("Errore nel database: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [HttpGet("{CFpaziente}")]
    public async Task<IActionResult> GetPaziente(string CFpaziente)
    {
        try
        {
            var paziente = await _context.Pazienti.FirstOrDefaultAsync(p=>p.CF==CFpaziente);
            if (paziente == null) return NotFound("CF errato.");
            return Ok(paziente);
        }
        catch (SqlException ex)
        {
            // Errore specifico di SQL Server
            return BadRequest("Errore nel database: " + ex.Message);
        }
        catch (Exception ex)
        {
            // Altri errori generici
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [HttpPost("aggiungi")]
    public async Task<IActionResult> AggiungiPaziente([FromBody] Paziente paziente)
    {
        try
        {
            var esiste = await _context.Pazienti.AnyAsync(u => u.CF == paziente.CF);
            if (esiste) return BadRequest("Paziente già registrato.");

            _context.Pazienti.Add(paziente);
            await _context.SaveChangesAsync();
            return Ok(paziente);
        }
        catch (DbUpdateException ex)
        {
            return BadRequest("Errore nel salvataggio: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
    [HttpGet("Reparto/{nomeReparto}")]
    public async Task<IActionResult> GetReparto(string nomeReparto)
    {
        try
        {
            var reparto= await _context.Reparti.FirstOrDefaultAsync(r => r.Nome == nomeReparto);
            if (reparto==null) return NotFound("Nome reparto non corretto");
            var pazienti = await _context.Pazienti.Where(p => p.IDReparto == reparto.ID).ToListAsync();
            if (!pazienti.Any()) return NotFound("Nessun paziente in questo reparto.");

            return Ok(pazienti);
        }
        catch (SqlException ex)
        {
            return BadRequest("Errore nel database: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
    /* api/Pazienti/modifica_dati_medici?matricolaMedico=100000&CFpaziente=AAAAAA00A00A000A*/
    [HttpPut("modifica_dati_medici")]
    public async Task<IActionResult> ModificaDatiMedico([FromBody] DatiMedici pazienteModifica, 
        [FromQuery] int matricolaMedico, [FromQuery] string CFpaziente)
    {
        try
        {
            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Matricola errata.");
            if (utente.Ruolo != "Medico") return BadRequest("Non hai accesso a questa sezione.");

            var paziente = await _context.Pazienti.FirstOrDefaultAsync(p=> p.CF==CFpaziente);
            if(paziente == null) return NotFound("CF errato.");
            if(paziente.IDReparto!=utente.IDReparto) 
                return BadRequest("Non puoi modificare i dati dei pazienti di un altro reparto.");

            paziente.DataDimissione = pazienteModifica.DataDimissione;
            paziente.Patologie = pazienteModifica.Patologie;
            paziente.Allergie = pazienteModifica.Allergie;
            paziente.AltreNote = pazienteModifica.AltreNote;
            await _context.SaveChangesAsync();

            return Ok("Paziente modificato con successo.");

        }
        catch (DbUpdateException ex)
        {
            return BadRequest("Errore nel salvataggio: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
    [HttpGet("da_ricoverare")]
    public async Task<IActionResult> GetPazientiDaRicoverare([FromQuery] DateOnly data, 
        [FromQuery] string nomeReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FirstOrDefaultAsync(r => r.Nome == nomeReparto);
            if (reparto == null) return NotFound("Nome reparto non corretto");
            var pazienti = await _context.Pazienti
                .Where(p => p.DataRicovero == data && p.NumeroLetto == 0 && p.IDReparto == reparto.ID).ToListAsync();
            if (pazienti == null) return NotFound("Nessun paziente da ricoverare per la data specificata.");

            return Ok(pazienti);
        }
        catch (SqlException ex)
        {
            return BadRequest("Errore nel database: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [HttpGet("da_dimettere")]
    public async Task<IActionResult> GetPazientiDaDimettere([FromQuery] DateOnly data,
        [FromQuery] string nomeReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FirstOrDefaultAsync(r => r.Nome == nomeReparto);
            if (reparto == null) return NotFound("Nome reparto non corretto");
            var pazienti = await _context.Pazienti
                .Where(p => p.DataRicovero == data && p.IDReparto == reparto.ID).ToListAsync();
            if (pazienti == null) return NotFound("Nessun paziente da dimettere per la data specificata.");

            return Ok(pazienti);
        }
        catch (SqlException ex)
        {
            return BadRequest("Errore nel database: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [HttpPut("modifica")]
    public async Task<IActionResult> ModificaDati([FromBody] DatiPaziente pazienteModifica, 
        [FromQuery] string CFpaziente, [FromQuery] int matricola)
    {
        try
        {
            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Matricola errata.");

            var paziente = await _context.Pazienti.FirstOrDefaultAsync(p => p.CF == CFpaziente);
            if (paziente == null) return NotFound("CF errato.");
            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi modificare i dati dei pazienti di un altro reparto.");

            if(pazienteModifica.DataNascita!=null) paziente.DataNascita = pazienteModifica.DataNascita.Value;
            if(pazienteModifica.CF!=null) paziente.CF = pazienteModifica.CF;
            if(pazienteModifica.Nome!=null) paziente.Nome = pazienteModifica.Nome;
            if(pazienteModifica.Cognome!=null) paziente.Cognome = pazienteModifica.Cognome;
            if(pazienteModifica.NumeroLetto!=null) paziente.NumeroLetto = pazienteModifica.NumeroLetto;
            if(pazienteModifica.IDReparto!=null) paziente.IDReparto = pazienteModifica.IDReparto.Value;
            if(pazienteModifica.LuogoNascita!=null) paziente.LuogoNascita = pazienteModifica.LuogoNascita;
            if(pazienteModifica.DataRicovero!=null) paziente.DataRicovero = pazienteModifica.DataRicovero.Value;
            if(pazienteModifica.MotivoRicovero!=null) paziente.MotivoRicovero = pazienteModifica.MotivoRicovero;
            await _context.SaveChangesAsync();

            return Ok("Paziente modificato con successo.");

        }
        catch (DbUpdateException ex)
        {
            return BadRequest("Errore nel salvataggio: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
    [HttpPut("ricovera_paziente")]
    public async Task<IActionResult> RicoveraPaziente([FromBody] DatiReparto ricoverato,
        [FromQuery] string CFpaziente, [FromQuery] int matricola)
    {
        try
        {
            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Matricola errata.");

            var paziente = await _context.Pazienti.FirstOrDefaultAsync(p => p.CF == CFpaziente);
            if (paziente == null) return NotFound("CF errato.");
            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi modificare i dati dei pazienti di un altro reparto.");

            if (ricoverato.IDReparto != null)
                paziente.IDReparto = ricoverato.IDReparto.Value;
            var reparto = await _context.Reparti.FindAsync(paziente.IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato.");

            if (ricoverato.NumeroLetto < 1 || ricoverato.NumeroLetto > reparto.NumeroLetti)
                return BadRequest($"Numero letto non valido. Per questo reparto devi inserire un numero da 1 a {reparto.NumeroLetti}.");
            var occupato = await _context.Pazienti.AnyAsync(p => p.IDReparto == ricoverato.IDReparto &&
                p.NumeroLetto == ricoverato.NumeroLetto &&
                p.DataDimissione == null &&
                p.CF != CFpaziente);
            if (occupato) return BadRequest("Il letto è già occupato da un altro paziente.");

            paziente.NumeroLetto = ricoverato.NumeroLetto;
            await _context.SaveChangesAsync();

            return Ok("Paziente modificato con successo.");
        }
        catch (DbUpdateException ex)
        {
            return BadRequest("Errore nel salvataggio: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
    [HttpDelete("dimetti")]
    public async Task<IActionResult> EliminaPaziente([FromQuery] int idPaziente)
    {
        try
        {
            var paziente = await _context.Pazienti.FindAsync(idPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");

            _context.Pazienti.Remove(paziente);
            await _context.SaveChangesAsync();

            return Ok("Paziente eliminato con successo.");
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio: " + ex.Message);
        }
        catch (SqlException ex)
        {
            return StatusCode(500, "Errore nel database: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
}
public class DatiMedici
{
    public DateOnly? DataDimissione { get; set; }
    public string? Patologie { get; set; }
    public string? Allergie { get; set; }
    public string? AltreNote { get; set; }
}
public class DatiPaziente
{
    public string? CF { get; set; }
    public string? Nome { get; set; }
    public string? Cognome { get; set; }
    public DateOnly? DataNascita { get; set; }
    public string? LuogoNascita { get; set; }
    public DateOnly? DataRicovero { get; set; }
    public string? MotivoRicovero { get; set; }
    public int? IDReparto { get; set; }
    public int? NumeroLetto { get; set; }
}
public class DatiReparto
{
    public int? IDReparto { get; set; }
    public int NumeroLetto { get; set; }
}

