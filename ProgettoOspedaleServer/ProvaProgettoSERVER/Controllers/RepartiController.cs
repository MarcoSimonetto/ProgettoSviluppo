using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Services;


namespace ProvaProgettoSERVER.Controllers;

[ApiController]
[Route("api/[controller]")]

public class RepartiController:ControllerBase
{
    private readonly OspedaleContext _context;

    public RepartiController(OspedaleContext context)
    {
        _context = context;
    }

    // Ritorna tutti i reparti memorizzati all'interno del database.
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var reparti = await _context.Reparti.ToListAsync();
            return Ok(reparti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    // Ritorna il reparto identificato dall'ID fornito nella richiesta
    [Authorize]
    [HttpGet("{IDReparto}")]
    public async Task<IActionResult> GetReparto(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato!");

            return Ok(reparto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    // Ritorna quanti letti sono liberi all'interno del reparto identificato dall'ID fornito nella richiesta
    [Authorize]
    [HttpGet("letti_liberi/{IDReparto}")]
    public async Task<IActionResult> LettiDisponibili(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null)
                return NotFound("Reparto non trovato!");

            int pazientiOccupanti = await _context.Pazienti.CountAsync(p => p.IDReparto == reparto.ID);
            int lettiDisponibili = reparto.NumeroLetti - pazientiOccupanti;

            return Ok(lettiDisponibili);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }

    }

    // Ritorna (se presente) il paziente che è attualmente ricovero nel numero letto fornito nella richiesta
    // del reparto identificato dall'ID fornito nella richiesta.
    [Authorize]
    [HttpGet("{IDReparto}/{NumeroLetto}")]
    public async Task<IActionResult> GetPazientePerLetto(int IDReparto, int NumeroLetto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null)
                return NotFound("Reparto non trovato!");

            if (NumeroLetto < 1 || NumeroLetto > reparto.NumeroLetti)
                return BadRequest($"Numero letto non valido! Devi inserire un numero da 1 a {reparto.NumeroLetti}.");

            var paziente = await _context.Pazienti
                .FirstOrDefaultAsync(p => p.IDReparto == IDReparto && p.NumeroLetto == NumeroLetto);

            return Ok(paziente);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    // Ritorna la lista dei letti liberi del reparto identificato dall'ID fornito nella richiesta
    [Authorize]
    [HttpGet("lista_letti_liberi/{IDReparto}")]
    public async Task<IActionResult> ListaLettiLiberi(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null)
                return NotFound("Reparto non trovato!");

            var oggi = DateOnly.FromDateTime(DateTime.Today);

            var lettiOccupati = await _context.Pazienti
                .Where(p => p.IDReparto == reparto.ID &&
                            p.DataRicovero <= oggi &&
                            (p.DataDimissione == null || p.DataDimissione >= oggi))
                .Select(p => p.NumeroLetto)
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToListAsync();

            var tuttiLetti = Enumerable.Range(1, reparto.NumeroLetti);

            var lettiLiberi = tuttiLetti.Except(lettiOccupati).ToList();

            return Ok(lettiLiberi);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

}
