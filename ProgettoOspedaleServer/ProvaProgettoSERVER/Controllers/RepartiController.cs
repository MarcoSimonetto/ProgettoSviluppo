using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var reparti = await _context.Reparti.ToListAsync();
            return Ok(reparti);
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
    [HttpGet("{IDReparto}")]
    public async Task<IActionResult> GetReparto(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null) return NotFound("Reparto non trovato.");

            return Ok(reparto);
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
    [HttpGet("letti_liberi/{IDReparto}")]
    public async Task<IActionResult> LettiDisponibili(int IDReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null)
                return NotFound("Reparto non trovato");

            int pazientiOccupanti = await _context.Pazienti.CountAsync(p => p.IDReparto == reparto.ID);
            int lettiDisponibili = reparto.NumeroLetti - pazientiOccupanti;

            return Ok(lettiDisponibili);
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
    [HttpGet("{IDReparto}/{NumeroLetto}")]
    public async Task<IActionResult> GetPazientePerLetto(int IDReparto, int NumeroLetto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(IDReparto);
            if (reparto == null)
                return NotFound("Reparto non trovato.");

            if (NumeroLetto < 1 || NumeroLetto > reparto.NumeroLetti)
                return BadRequest($"Numero letto non valido. Devi inserire un numero da 1 a {reparto.NumeroLetti}.");

            var paziente = await _context.Pazienti
                .FirstOrDefaultAsync(p => p.IDReparto == IDReparto && p.NumeroLetto == NumeroLetto);

            if (paziente == null)
                return Ok("Questo letto non è occupato.");

            return Ok(paziente);
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
