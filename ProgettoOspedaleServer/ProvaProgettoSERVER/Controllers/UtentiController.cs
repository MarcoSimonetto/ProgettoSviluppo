using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

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
            if (!ValidatePassword(nuovoUtente.Password, out string errorMessage))
            {
                return BadRequest(errorMessage);
            }
            if (esiste) return BadRequest("Utente già registrato!");

            _context.Utenti.Add(nuovoUtente);
            await _context.SaveChangesAsync();

            return Ok(nuovoUtente.Matricola);
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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginData utenteLogin)
    {
        try
        {
            var utente = await _context.Utenti
            .FirstOrDefaultAsync(u => u.Matricola == utenteLogin.Matricola && u.Password == utenteLogin.Password);

            if (utente == null) return Unauthorized("Credenziali non valide!");

            var loggato = new
            {
                Mat = utente.Matricola,
                RuoloMat = utente.Ruolo,
                Reparto = utente.IDReparto,
                Pass = utente.Password
            };

            return Ok(loggato);
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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var utenti = await _context.Utenti.ToListAsync();
            return Ok(utenti);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Errore imprevisto: " + ex.Message);
        }
    }

    [HttpGet("check_ruolo_medico")]
    [Authorize(Roles = "Medico")]
    public IActionResult CheckMedico()
    {
        return Ok();
    }

    [HttpGet("check_ruolo_infermiere")]
    [Authorize(Roles = "Infermiere")]
    public IActionResult CheckInfermiere()
    {
        return Ok();
    }

    [HttpGet("check_ruolo_medico_infermiere")]
    [Authorize(Roles = "Medico,Infermiere")]
    public IActionResult CheckMedicoInfermiere()
    {
        return Ok();
    }

    // il controllo della password viene fatto nel form del client con i required del modello (vedi modello Utente.cs MVC)
    // nonostante ciò presuppondno possa fallire in qualche modo aggiungiamo un'ulteriore controllo sulla password anche dal server 
    private bool ValidatePassword(string password, out string ErrorMessage)
    {
        var input = password;
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            throw new Exception("Password vuota");
        }

        var hasNumber = new Regex(@"[0-9]+");
        var hasUpperChar = new Regex(@"[A-Z]+");
        var hasMiniMaxChars = new Regex(@".{8,15}");
        var hasLowerChar = new Regex(@"[a-z]+");

        if (!hasLowerChar.IsMatch(input))
        {
            ErrorMessage = "Password deve contenere almeno un carattere minuscolo.";
            return false;
        }
        else if (!hasUpperChar.IsMatch(input))
        {
            ErrorMessage = "Password deve contenere almeno un carattere maiuscolo.";
            return false;
        }
        else if (!hasMiniMaxChars.IsMatch(input))
        {
            ErrorMessage = "Password non deve essere meno di 8 caratteri e piu di 15.";
            return false;
        }
        else if (!hasNumber.IsMatch(input))
        {
            ErrorMessage = "Password deve contenere almeno un numero";
            return false;
        }

        else
        {
            return true;
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


