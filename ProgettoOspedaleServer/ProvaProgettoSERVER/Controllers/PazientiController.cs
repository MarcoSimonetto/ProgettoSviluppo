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
    public readonly DateOnly oggi = DateOnly.FromDateTime(DateTime.Today);

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
            if (paziente == null) return NotFound("Paziente non trovato!");
            return Ok(paziente);
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
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
                return Unauthorized("Matricola non valida nei claims!");

            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Utente non trovato!");

            var esiste = await _context.Pazienti.AnyAsync(u => u.CF == paziente.CF);
            if (esiste) return BadRequest("Paziente già registrato!");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi aggiungere un paziente in un altro reparto!");

            var reparto = await _context.Reparti.FindAsync(paziente.IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");

            if (paziente.DataNascita > oggi)
                return BadRequest("Data di nascita non valida!");

            if (paziente.DataDimissione != null && (paziente.DataRicovero > paziente.DataDimissione))
                return BadRequest("Hai inserito data di ricovero e di dimissione errate!");

            if (paziente.NumeroLetto == null)
            {
                if (paziente.DataRicovero <= oggi)
                    return BadRequest("Devi inserire una data futura come data di ricovero!");
                paziente.NumeroLetto = 0;
            }
            else
            {
                if (paziente.DataRicovero > oggi)
                    return BadRequest("Devi inserire la data odierna come data di ricovero!");
                if (paziente.NumeroLetto < 1 || paziente.NumeroLetto > reparto.NumeroLetti)
                    return BadRequest($"Numero letto non valido! Per questo reparto devi inserire un numero da 1 a {reparto.NumeroLetti}.");

                var occupato = await _context.Pazienti.AnyAsync(p => p.IDReparto == paziente.IDReparto &&
                    p.NumeroLetto == paziente.NumeroLetto &&
                    (p.DataDimissione == null || p.DataDimissione >= oggi) &&
                    p.ID != paziente.ID);
                if (occupato) return BadRequest("Il letto è già occupato da un altro paziente!");
            }

            _context.Pazienti.Add(paziente);
            await _context.SaveChangesAsync();
            return Ok(paziente);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio dei dati: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize]
    [HttpGet("Reparto/{IDReparto}")]
    public async Task<IActionResult> PazientiRicoverati(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");

            var pazienti = await _context.Pazienti
            .Where(p => p.IDReparto == reparto.ID &&
                        p.DataRicovero <= oggi &&
                        (p.DataDimissione == null || p.DataDimissione >= oggi) &&
                        p.NumeroLetto!=0)
            .ToListAsync();

            return Ok(pazienti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize(Roles = "Medico")]
    [HttpPut("modifica_dati_medici/{IDPaziente}")]
    public async Task<IActionResult> ModificaDatiMedici([FromBody] DatiMedici pazienteModifica, int IDPaziente)
    {
        try
        {
            var matricolaMedicoClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaMedicoClaim, out var matricolaMedico))
                return Unauthorized("Matricola non valida nei claims!");

            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Utente non trovato!");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato!");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi modificare i dati medici dei pazienti di un altro reparto!");

            if (pazienteModifica.DataRicovero != null)
                paziente.DataRicovero = pazienteModifica.DataRicovero.Value;

            if (paziente.DataRicovero > paziente.DataDimissione && paziente.DataDimissione != null)
                return BadRequest("Hai inserito data di ricovero e di dimissione errate!");
            paziente.DataDimissione = pazienteModifica.DataDimissione;

            if (pazienteModifica.MotivoRicovero != null) paziente.MotivoRicovero = pazienteModifica.MotivoRicovero;
            paziente.Patologie = pazienteModifica.Patologie;
            paziente.Allergie = pazienteModifica.Allergie;
            paziente.AltreNote = pazienteModifica.AltreNote;

            await _context.SaveChangesAsync();  

            return Ok("Paziente modificato con successo.");

        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio dei dati: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize(Roles = "Medico,Infermiere")]
    [HttpPut("modifica/{IDPaziente}")]
    public async Task<IActionResult> ModificaDati([FromBody] DatiPaziente pazienteModifica, int IDPaziente)
    {
        try
        {
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
                return Unauthorized("Matricola non valida nei claims!");

            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Utente non trovato!");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato!");
            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi modificare i dati personali dei pazienti di un altro reparto!");

            if (pazienteModifica.DataNascita > oggi)
                return BadRequest("Data di nascita non valida!");
            if (pazienteModifica.DataNascita != null) paziente.DataNascita = pazienteModifica.DataNascita.Value;

            if (pazienteModifica.CF != null) paziente.CF = pazienteModifica.CF;
            if (pazienteModifica.Nome != null) paziente.Nome = pazienteModifica.Nome;
            if (pazienteModifica.Cognome != null) paziente.Cognome = pazienteModifica.Cognome;
            if (pazienteModifica.LuogoNascita != null) paziente.LuogoNascita = pazienteModifica.LuogoNascita;
            await _context.SaveChangesAsync();

            return Ok("Paziente modificato con successo.");

        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio dei dati: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize]
    [HttpGet("da_ricoverare/{IDReparto}/{data}")]
    public async Task<IActionResult> GetPazientiDaRicoverare(DateOnly data, int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");
            var pazienti = await _context.Pazienti
                .Where(p => p.DataRicovero == data && p.NumeroLetto == 0 && p.IDReparto == reparto.ID).ToListAsync();

            return Ok(pazienti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize]
    [HttpGet("da_ricoverare/{IDReparto}/oggi")]
    public async Task<IActionResult> GetPazientiDaRicoverareOggi(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");
            var pazienti = await _context.Pazienti
                .Where(p => p.DataRicovero == oggi && p.NumeroLetto == 0 && p.IDReparto == reparto.ID).ToListAsync();

            return Ok(pazienti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize]
    [HttpGet("da_dimettere/{IDReparto}/{data}")]
    public async Task<IActionResult> GetPazientiDaDimettere(DateOnly data, int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");
            var pazienti = await _context.Pazienti
                .Where(p => p.DataDimissione == data && p.IDReparto == reparto.ID).ToListAsync();

            return Ok(pazienti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize]
    [HttpGet("da_dimettere/{IDReparto}/oggi")]
    public async Task<IActionResult> GetPazientiDaDimettereOggi(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");
            var pazienti = await _context.Pazienti
                .Where(p => p.DataDimissione == oggi && p.IDReparto == reparto.ID).ToListAsync();

            return Ok(pazienti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize(Roles = "Medico")]
    [HttpPut("ricovera/{IDPaziente}/{NumeroLetto}")]
    public async Task<IActionResult> RicoveraPaziente(int IDPaziente, int NumeroLetto)
    {
        try
        {
            var matricolaMedicoClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaMedicoClaim, out var matricolaMedico))
                return Unauthorized("Matricola non valida nei claims!");

            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Utente non trovato!");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato!");
            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi ricoverare i pazienti di un altro reparto!");

            var reparto = await _context.Reparti.FindAsync(paziente.IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");

            if (paziente.NumeroLetto != 0)
                return BadRequest("Il paziente è già stato ricoverato!");

            if (NumeroLetto < 1 || NumeroLetto > reparto.NumeroLetti)
                return BadRequest($"Numero letto non valido! Per questo reparto devi inserire un numero da 1 a {reparto.NumeroLetti}.");

            var occupato = await _context.Pazienti.AnyAsync(p => p.IDReparto == paziente.IDReparto &&
                p.NumeroLetto == NumeroLetto &&
                (p.DataDimissione == null || p.DataDimissione >= oggi) &&
                p.ID != IDPaziente);
            if (occupato) return BadRequest("Il letto è già occupato da un altro paziente!");

            paziente.NumeroLetto = NumeroLetto;
            await _context.SaveChangesAsync();

            return Ok("Paziente ricoverato con successo.");
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio dei dati: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize(Roles = "Medico")]
    [HttpDelete("dimetti/{IDPaziente}")]
    public async Task<IActionResult> EliminaPaziente(int IDPaziente)
    {
        try
        {
            var matricolaMedicoClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaMedicoClaim, out var matricolaMedico))
                return Unauthorized("Matricola non valida nei claims!");

            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Utente non trovato!");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato!");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi dimettere i pazienti di un altro reparto!");

            if (paziente.DataDimissione != null && paziente.DataDimissione > oggi)
                return BadRequest("Non puoi dimettere questo paziente perchè la sua data di dimissione non è oggi!");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var terapie = await _context.Terapie
                    .Where(t => t.IDPaziente == IDPaziente)
                    .ToListAsync();

                foreach (var terapia in terapie)
                {
                    var somministrazioni = await _context.Somministrazioni
                        .Where(s => s.IDTerapia == terapia.ID)
                        .ToListAsync();

                    _context.Somministrazioni.RemoveRange(somministrazioni);
                }

                _context.Terapie.RemoveRange(terapie);
                await _context.SaveChangesAsync();

                _context.Pazienti.Remove(paziente);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok("Paziente e dati collegati eliminati con successo.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Errore durante l'eliminazione: {ex.Message}. Dettagli: {ex.InnerException?.Message}");
            }
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio dei dati: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize(Roles = "Medico, Infermiere")]
    [HttpPut("trasferimento/{IDPaziente}")]
    public async Task<IActionResult> TrasferimentoPaziente([FromBody] DatiReparto trasferimento, 
        int IDPaziente)
    {
        try
        {
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
                return Unauthorized("Matricola non valida nei claims!");

            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Utente non trovato!");

            var paziente = await _context.Pazienti.FindAsync(IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato!");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puo trasferire un paziente di un altro reparto!");

            if (trasferimento.IDReparto == null)
            {
                if (trasferimento.NumeroLetto == paziente.NumeroLetto)
                    return BadRequest("Hai inserito il numero del letto in cui è attualmente ricoverato il paziente!");
                trasferimento.IDReparto = paziente.IDReparto;
            }

            var reparto = await _context.Reparti.FindAsync(trasferimento.IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato.");

            if (trasferimento.NumeroLetto < 1 || trasferimento.NumeroLetto > reparto.NumeroLetti)
                return BadRequest($"Numero letto non valido! Per questo reparto devi inserire un numero da 1 a {reparto.NumeroLetti}.");

            var occupato = await _context.Pazienti.AnyAsync(p => p.IDReparto == trasferimento.IDReparto &&
            p.NumeroLetto == trasferimento.NumeroLetto &&
            (p.DataDimissione == null || p.DataDimissione >= oggi) &&
            p.ID != IDPaziente);
            if (occupato) return BadRequest("Il letto è già occupato da un altro paziente!");

            if (trasferimento.IDReparto != paziente.IDReparto)
                paziente.IDReparto = trasferimento.IDReparto.Value;
            paziente.NumeroLetto = trasferimento.NumeroLetto;

            await _context.SaveChangesAsync();

            return Ok("Trasferimento paziente avvenuto con successo.");

        }
        catch (DbUpdateException ex)
        {
            return StatusCode(500, "Errore nel salvataggio dei dati: " + ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }
}
public class DatiMedici
{
    public DateOnly? DataRicovero { get; set; }
    public string? MotivoRicovero { get; set; }
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
}
public class DatiReparto
{
    public int? IDReparto { get; set; }
    public int NumeroLetto { get; set; }
}

