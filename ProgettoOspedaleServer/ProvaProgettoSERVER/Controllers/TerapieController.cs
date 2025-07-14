using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;
using System.Threading.Tasks;

namespace ProvaProgettoSERVER.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TerapieController : ControllerBase
{
    private readonly OspedaleContext _context;

    public TerapieController(OspedaleContext context)
    {
        _context = context;
    }

    [HttpPost("assegna")]
    public async Task<IActionResult> AssegnaTerapia([FromBody] Terapia terapia)
    {
        try
        {
            var utente = await _context.Utenti.FindAsync(terapia.MatricolaMedico);
            if (utente == null) return NotFound("Matricola errata.");
            if (utente.Ruolo != "Medico") 
                return BadRequest("Non hai i requisiti per assegnare una terapia.");

            var paziente = await _context.Pazienti.FindAsync(terapia.IDPaziente);
            if (paziente == null) return NotFound("ID errato.");
            if (utente.IDReparto != paziente.IDReparto) 
                return BadRequest("Non puoi assegnare una terapia ad un paziente di un altro reparto.");

            _context.Terapie.Add(terapia);
            await _context.SaveChangesAsync();
            return Ok(terapia);
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

    [HttpPut("modifica")]
    public async Task<IActionResult> ModificaTerapia([FromQuery] int id, [FromQuery] int matricolaMedico, [FromBody] Terapia nuovaTerapia)
    {
        try
        {
            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Matricola errata.");
            if (utente.Ruolo != "Medico") return BadRequest("Non hai i requisiti per modificare una terapia.");

            var terapia = await _context.Terapie.FindAsync(id);
            if (terapia == null) return NotFound("Terapia non trovata");

            terapia.Farmaco = nuovaTerapia.Farmaco;
            terapia.Dosaggio = nuovaTerapia.Dosaggio;
            terapia.OrarioSomministrazione = nuovaTerapia.OrarioSomministrazione;
            terapia.DataInizio = nuovaTerapia.DataInizio;
            terapia.DataFine = nuovaTerapia.DataFine;

            await _context.SaveChangesAsync();
            return Ok(terapia);
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

    [HttpGet("{idPaziente}")]
    public async Task<IActionResult> TerapiePaziente(int idPaziente)
    {
        try
        {
            var paziente = await _context.Pazienti.FindAsync(idPaziente);
            if (paziente == null) return NotFound("Paziente non trovato.");
            var terapie = await _context.Terapie.Where(t => t.IDPaziente == idPaziente).ToListAsync();
            if (!terapie.Any()) return NotFound("Non ci sono terapie assegnate a questo paziente");
            return Ok(terapie);
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

    [HttpDelete("rimuovi")]
    public async Task<IActionResult> EliminaTerapia([FromQuery] int idTerapia, [FromQuery] int matricolaMedico)
    {
        try
        {
            var utente = await _context.Utenti.FindAsync(matricolaMedico);
            if (utente == null) return NotFound("Matricola non trovata.");
            if (utente.Ruolo != "Medico") return BadRequest("Non hai i requisiti per eliminare una terapia.");

            var terapia = await _context.Terapie.FindAsync(idTerapia);
            if (terapia == null) return NotFound("Terapia non trovata.");

            var paziente = await _context.Pazienti.FirstOrDefaultAsync(p => p.ID == terapia.IDPaziente);
            if (paziente==null) return NotFound("Paziente non trovato.");
            if (paziente.IDReparto!=utente.IDReparto) 
                return BadRequest("Non puoi rimuovere le terapia di pazienti di altri reparti.");

            /*var somministrazioni = await _context.Somministrazioni
                .Where(s => s.IDTerapia == idTerapia)
                .ToListAsync();
            _context.Somministrazioni.RemoveRange(somministrazioni);*/

            _context.Terapie.Remove(terapia);
            await _context.SaveChangesAsync();

            return Ok("Terapia eliminata con successo.");
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
