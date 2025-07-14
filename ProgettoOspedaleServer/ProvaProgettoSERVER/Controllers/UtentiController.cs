using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;
using System.ComponentModel.DataAnnotations;

namespace ProvaProgettoSERVER.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UtentiController : ControllerBase
{
    private readonly OspedaleContext _context;

    public UtentiController(OspedaleContext context)
    {
        _context = context;
    }

    [HttpPost("registrazione")]
    public async Task<IActionResult> Registrazione([FromBody] Utente nuovoUtente)
    {
        try
        {
            var esiste = await _context.Utenti.AnyAsync(u => u.CF == nuovoUtente.CF);
            if (esiste) return BadRequest("Utente già registrato.");

            _context.Utenti.Add(nuovoUtente);
            await _context.SaveChangesAsync();

            return Ok(nuovoUtente.Matricola);
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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginData utenteLogin)
    {
        try
        {
            var utente = await _context.Utenti
            .FirstOrDefaultAsync(u => u.Matricola == utenteLogin.Matricola && u.Password == utenteLogin.Password);

            if (utente == null) return Unauthorized("Credenziali non valide.");

            var loggato = new
            {   
                Mat = utente.Matricola,
                RuoloMat = utente.Ruolo,
                Reparto = utente.IDReparto,
                Pass = utente.Password
            };

            return Ok(loggato);
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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var utenti = await _context.Utenti.ToListAsync();
            return Ok(utenti);
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
public class LoginData
{
    [Required]
    public int Matricola { get; set; }
    [Required]
    public string Password { get; set; }
}


