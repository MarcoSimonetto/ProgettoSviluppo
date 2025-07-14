using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;

namespace ProvaProgettoSERVER.Controllers;

[ApiController]
[Route("api/[controller]")]

public class SomministrazioniController : ControllerBase
{
    private readonly OspedaleContext _context;

    public SomministrazioniController(OspedaleContext context)
    {
        _context = context;
    }

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

    [HttpGet("verifica")]
    public async Task<IActionResult> GetSomministrazione([FromQuery] int idTerapia, [FromQuery] DateOnly data)
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

    [HttpPost("aggiungi")]
    public async Task<IActionResult> AggiungiSomministrazione([FromBody] Somministrazione nuova)
    {
        try
        {
            var infermiereEsiste = await _context.Utenti.AnyAsync(u => u.Matricola == nuova.MatricolaInfermiere && u.Ruolo == "infermiere");
            if (!infermiereEsiste)
                return NotFound("Infermiere non trovato.");

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

}
