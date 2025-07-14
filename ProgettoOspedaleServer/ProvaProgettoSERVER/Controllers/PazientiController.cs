using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;
using System.Security.Claims;

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

    [Authorize]
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

    [Authorize]
    [HttpGet("{IDPaziente}")]
    public async Task<IActionResult> GetPaziente(int IDPaziente)
    {
        try
        {
            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");
            return Ok(paziente);
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

    [Authorize(Roles = "Medico,Infermiere")]
    [HttpPost("aggiungi")]
    public async Task<IActionResult> AggiungiPaziente([FromBody] Paziente paziente)
    {
        try
        {
            var esiste = await _context.Pazienti.AnyAsync(u => u.CF == paziente.CF);
            if (esiste) return BadRequest("Paziente già registrato.");

            if (paziente.NumeroLetto == null)
                paziente.NumeroLetto = 0;

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

    [Authorize]
    [HttpGet("Reparto/{IDReparto}")]
    public async Task<IActionResult> GetReparto(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto==null) return NotFound("Reparto non trovato.");
            var pazienti = await _context.Pazienti.Where(p => p.IDReparto == reparto.ID).ToListAsync();
            if (!pazienti.Any()) return NotFound("Nessun paziente ricoverato in questo reparto.");

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

    [Authorize(Roles = "Medico")]
    [HttpPut("modifica_dati_medici")]
    public async Task<IActionResult> ModificaDatiMedico([FromBody] DatiMedici pazienteModifica,
    [FromQuery] int IDPaziente)
    {
        try
        {
            var matricolaMedicoClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaMedicoClaim, out var matricolaMedico))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }

            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Utente non trovato.");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi modificare i dati dei pazienti di un altro reparto!");

            if (pazienteModifica.DataDimissione!=null) paziente.DataDimissione = pazienteModifica.DataDimissione;
            if(pazienteModifica.Patologie!=null) paziente.Patologie = pazienteModifica.Patologie;
            if (pazienteModifica.Allergie!=null) paziente.Allergie = pazienteModifica.Allergie;
            if (pazienteModifica.AltreNote!=null) paziente.AltreNote = pazienteModifica.AltreNote;
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

    [Authorize]
    [HttpGet("da_ricoverare")]
    public async Task<IActionResult> GetPazientiDaRicoverare([FromQuery] DateOnly data, 
        [FromQuery] int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato.");
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

    [Authorize]
    [HttpGet("da_dimettere")]
    public async Task<IActionResult> GetPazientiDaDimettere([FromQuery] DateOnly data,
        [FromQuery] int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato.");
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

    [Authorize(Roles = "Medico,Infermiere")]
    [HttpPut("modifica")]
    public async Task<IActionResult> ModificaDati([FromBody] DatiPaziente pazienteModifica, 
        [FromQuery] int IDPaziente)
    {
        try
        {
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }
            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Utente non trovato.");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");
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

    [Authorize(Roles = "Medico")]
    [HttpPut("ricovera_paziente")]
    public async Task<IActionResult> RicoveraPaziente([FromBody] DatiReparto ricoverato,
        [FromQuery] int IDPaziente)
    {
        try
        {
            var matricolaMedicoClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaMedicoClaim, out var matricolaMedico))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }

            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Utente non trovato.");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");
            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi ricoverare i pazienti di un altro reparto!");

            //Il reparto è già stato settato in fase di accettazione del paziente
            //Controllo un'eventuale cambiamento del reparto in cui ricoverarlo
            if (ricoverato.IDReparto != null)
                paziente.IDReparto = ricoverato.IDReparto.Value;
            var reparto = await _context.Reparti.FindAsync(paziente.IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato.");

            if (ricoverato.NumeroLetto < 1 || ricoverato.NumeroLetto > reparto.NumeroLetti)
                return BadRequest($"Numero letto non valido. Per questo reparto devi inserire un numero da 1 a {reparto.NumeroLetti}.");

            var oggi = DateOnly.FromDateTime(DateTime.Today);
            var occupato = await _context.Pazienti.AnyAsync(p => p.IDReparto == ricoverato.IDReparto &&
                p.NumeroLetto == ricoverato.NumeroLetto &&
                (p.DataDimissione == null || p.DataDimissione >= oggi) &&
                p.ID != IDPaziente);
            if (occupato) return BadRequest("Il letto è già occupato da un altro paziente.");

            paziente.NumeroLetto = ricoverato.NumeroLetto;
            await _context.SaveChangesAsync();

            return Ok("Paziente ricoverato con successo.");
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

    [Authorize(Roles = "Medico")]
    [HttpDelete("dimetti")]
    public async Task<IActionResult> EliminaPaziente([FromQuery] int IDPaziente)
    {
        try
        {
            var matricolaMedicoClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaMedicoClaim, out var matricolaMedico))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }

            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Utente non trovato.");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi dimettere i pazienti di un altro reparto!");

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

