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

    [HttpGet("{idReparto}")]
    public async Task<IActionResult> GetReparto(int idReparto)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(idReparto);
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

    [HttpGet("letti-liberi")]
    public async Task<IActionResult> LettiDisponibili([FromQuery] int id)
    {
        try
        {
            var reparto = await _context.Reparti.FindAsync(id);
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
}
