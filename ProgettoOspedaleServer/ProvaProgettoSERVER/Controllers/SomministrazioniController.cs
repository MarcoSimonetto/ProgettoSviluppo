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

public class SomministrazioniController : ControllerBase
{
    private readonly OspedaleContext _context;
    public DateOnly oggi = DateOnly.FromDateTime(DateTime.Today);

    public SomministrazioniController(OspedaleContext context)
    {
        _context = context;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var somministrazioni = await _context.Somministrazioni.ToListAsync();
            return Ok(somministrazioni);
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

    [Authorize]
    [HttpGet("verifica/{idTerapia}/{data}")]
    public async Task<IActionResult> GetSomministrazione(int idTerapia, DateOnly data)
    {
        try
        {
            var terapia = await _context.Terapie.FindAsync(idTerapia);
            if (terapia == null) return NotFound("ID Terapia errato.");

            var paziente = await _context.Pazienti.FirstOrDefaultAsync(p => p.ID == terapia.IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");

            if (data < paziente.DataRicovero ||
                (paziente.DataDimissione.HasValue && data > paziente.DataDimissione.Value))
            {
                return BadRequest("La data specificata non è compresa tra quella di ricovero e quella di dimissione.");
            }

            var somministrazione = await _context.Somministrazioni
                .Where(s => s.IDTerapia==idTerapia && s.Data==data)
                .ToListAsync();

            if (!somministrazione.Any())
                return NotFound("La terapia non è stata somministrata nella data specificata.");
            
            return Ok("La terapia è stata somministrata nella data specificata." + somministrazione);
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

    [Authorize(Roles = "Infermiere")]
    [HttpPost("aggiungi")]
    public async Task<IActionResult> AggiungiSomministrazione([FromBody] Somministrazione nuova)
    {
        try
        {
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }
            
            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Matricola non trovata.");

            if (nuova.MatricolaUtente != matricola)
                return BadRequest("Non puoi passare la matricola di un altro Infermiere!");

            var terapia = await _context.Terapie.FindAsync(nuova.IDTerapia);
            if (terapia == null) return NotFound("Terapia non trovata.");

            var paziente = await _context.Pazienti.FindAsync(terapia.IDPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");

            if (paziente.IDReparto != utente.IDReparto)
                return BadRequest("Non puoi somministrare la terapia di pazienti di altri reparti!");

            var somministrazioneEsiste = await _context.Somministrazioni
                .AnyAsync(s => s.Data == nuova.Data && s.IDTerapia == nuova.IDTerapia);
            if (somministrazioneEsiste)
                return BadRequest("La terapia è già stata somministrata.");

            _context.Somministrazioni.Add(nuova);
            await _context.SaveChangesAsync();

            return Ok(nuova);
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
    [HttpGet("oggi/{IDReparto}")]
    public async Task<IActionResult> SomministrazioniOdierne(int IDReparto)
    {
        try
        {
            var somministrazioni = await _context.Somministrazioni
                .Where(s => s.Data == oggi)
                .ToListAsync();

            if (!somministrazioni.Any())
                return Ok("Nessuna terapia somministrata oggi.");

            var terapiaIds = somministrazioni.Select(s => s.IDTerapia).ToList();
            var terapie = await _context.Terapie
                .Where(t => terapiaIds.Contains(t.ID))
                .ToListAsync();

            // Carico tutti i pazienti relativi alle terapie
            var pazienteIds = terapie.Select(t => t.IDPaziente).Distinct().ToList();
            var pazienti = await _context.Pazienti
                .Where(p => pazienteIds.Contains(p.ID) && p.IDReparto == IDReparto)
                .ToListAsync();

            // Filtra le somministrazioni legate a terapie dei pazienti del reparto
            var somministrazioniFiltrate = somministrazioni
                .Where(s =>
                {
                    var terapia = terapie.FirstOrDefault(t => t.ID == s.IDTerapia);
                    return terapia != null && pazienti.Any(p => p.ID == terapia.IDPaziente);
                })
                .ToList();

            if (!somministrazioniFiltrate.Any())
                return Ok("Nessuna terapia somministrata oggi.");

            return Ok(somministrazioniFiltrate);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [Authorize]
    [HttpGet("oggi/ancora_in_orario/{IDReparto}")]
    public async Task<IActionResult> SomministrazioniNonEseguiteMaAncoraInTempo(int IDReparto)
    {
        try
        {
            var oggi = DateOnly.FromDateTime(DateTime.Today);
            var oraCorrente = TimeOnly.FromDateTime(DateTime.Now);

            var terapieAncoraInTempo = await _context.Terapie
                .Where(t => t.DataInizio <= oggi && oggi <= t.DataFine && t.OrarioSomministrazione > oraCorrente)
                .ToListAsync();

            var somministrazioniEffettuate = await _context.Somministrazioni
                .Where(s => s.Data == oggi)
                .Select(s => s.IDTerapia)
                .ToListAsync();

            var pazienti = await _context.Pazienti.ToListAsync();

            var daSomministrareInTempo = terapieAncoraInTempo
                .Where(t => !somministrazioniEffettuate.Contains(t.ID))
                .Where(t => pazienti.Any(p => p.ID == t.IDPaziente && p.IDReparto == IDReparto))
                .Select(t =>
                {
                    var paziente = pazienti.First(p => p.ID == t.IDPaziente);
                    return new
                    {
                        TerapiaID = t.ID,
                        NomeFarmaco = t.Farmaco,
                        Paziente = $"{paziente.Nome} {paziente.Cognome}",
                        Letto = paziente.NumeroLetto,
                        OrarioPrevisto = t.OrarioSomministrazione.ToString("HH:mm"),
                        Stato = "In Orario"
                    };
                })
                .ToList();

            if (!daSomministrareInTempo.Any())
                return Ok("Sono state somministrate tutte le terapie per oggi.");

            return Ok(daSomministrareInTempo);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }


    [Authorize]
    [HttpGet("oggi/in_ritardo/{IDReparto}")]
    public async Task<IActionResult> SomministrazioniNonEseguiteInRitardo(int IDReparto)
    {
        try
        {
            var oggi = DateOnly.FromDateTime(DateTime.Today);
            var oraAttuale = TimeOnly.FromDateTime(DateTime.Now);

            var terapieAttive = await _context.Terapie
                .Where(t => t.DataInizio <= oggi && oggi <= t.DataFine && t.OrarioSomministrazione < oraAttuale)
                .ToListAsync();

            var somministrazioniEffettuate = await _context.Somministrazioni
                .Where(s => s.Data == oggi)
                .Select(s => s.IDTerapia)
                .ToListAsync();

            var pazienti = await _context.Pazienti.ToListAsync();

            var terapieScadute = terapieAttive
                .Where(t => !somministrazioniEffettuate.Contains(t.ID))
                .Where(t => pazienti.Any(p => p.ID == t.IDPaziente && p.IDReparto == IDReparto))
                .Select(t =>
                {
                    var paziente = pazienti.First(p => p.ID == t.IDPaziente);
                    return new 
                    {
                        TerapiaID = t.ID,
                        NomeFarmaco = t.Farmaco,
                        Paziente = $"{paziente.Nome} {paziente.Cognome}",
                        Letto = paziente.NumeroLetto,
                        OrarioPrevisto = t.OrarioSomministrazione.ToString("HH:mm"),
                        Stato = "In Ritardo"
                    };
                })
                .ToList();

            if (!terapieScadute.Any())
                return Ok("Non ci sono terapie da somministrare in ritardo.");

            return Ok(terapieScadute);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

}
