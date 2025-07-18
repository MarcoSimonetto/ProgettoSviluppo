﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProvaProgettoSERVER.Models;
using ProvaProgettoSERVER.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProvaProgettoSERVER.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TerapieController : ControllerBase
{
    private readonly OspedaleContext _context;
    public DateOnly oggi = DateOnly.FromDateTime(DateTime.Today);

    public TerapieController(OspedaleContext context)
    {
        _context = context;
    }

    [Authorize(Roles = "Medico")]
    [HttpPost("assegna")]
    public async Task<IActionResult> AssegnaTerapia([FromBody] Terapia terapia)
    {
        try
        {
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }

            var utente = await _context.Utenti.FindAsync(matricola);
            if (utente == null) return NotFound("Matricola errata.");

            if(matricola!=terapia.MatricolaMedico)
                return BadRequest("Devi passare la tua matricola quando assegni una terapia!");

            var paziente = await _context.Pazienti.FindAsync(terapia.IDPaziente);
            if (paziente == null) return NotFound("ID errato.");
            if (utente.IDReparto != paziente.IDReparto) 
                return BadRequest("Non puoi assegnare una terapia ad un paziente di un altro reparto!");

            if (terapia.DataInizio>terapia.DataFine)
                return BadRequest("Hai una inserito una data di inizio che viene dopo la data di fine!");
            if (terapia.DataInizio < paziente.DataRicovero || terapia.DataFine < paziente.DataRicovero)
                return BadRequest("La data di inizio o fine terapia non può precedere la data di ricovero del paziente.");
            if (paziente.DataDimissione != null)
            {
                if (terapia.DataInizio > paziente.DataDimissione || terapia.DataFine > paziente.DataDimissione)
                    return BadRequest("La data di inizio o fine terapia non può essere dopo la data di dimissione del paziente!");
            }
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

    [Authorize(Roles = "Medico")]
    [HttpPut("modifica/{IDTerapia}")]
    public async Task<IActionResult> ModificaTerapia(int IDTerapia, [FromBody] DatiTerapia nuovaTerapia)
    {
        try
        {
            var matricolaClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(matricolaClaim, out var matricola))
            {
                return Unauthorized("Matricola non valida nei claims.");
            }

            var terapia = await _context.Terapie.FindAsync(IDTerapia);
            if (terapia == null) return NotFound("Terapia non trovata");

            if (matricola != terapia.MatricolaMedico)
                terapia.MatricolaMedico = matricola;

            if(nuovaTerapia.Farmaco!=null) terapia.Farmaco = nuovaTerapia.Farmaco;
            if(nuovaTerapia.Dosaggio!=null) terapia.Dosaggio = nuovaTerapia.Dosaggio;
            if(nuovaTerapia.OrarioSomministrazione!=null) terapia.OrarioSomministrazione = nuovaTerapia.OrarioSomministrazione.Value;
            if(nuovaTerapia.DataInizio!=null) terapia.DataInizio = nuovaTerapia.DataInizio.Value;
            if(nuovaTerapia.DataFine!=null) terapia.DataFine = nuovaTerapia.DataFine.Value;

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

    [Authorize]
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

    [Authorize(Roles = "Medico")]
    [HttpDelete("rimuovi/{idTerapia}")]
    public async Task<IActionResult> EliminaTerapia(int idTerapia)
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

            var terapia = await _context.Terapie.FindAsync(idTerapia);
            if (terapia == null) return NotFound("Terapia non trovata.");

            var paziente = await _context.Pazienti.FindAsync(terapia.IDPaziente);
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

    [Authorize]
    [HttpGet("oggi/{IDReparto}")]
    public async Task<IActionResult> TerapieDelGiorno(int IDReparto)
    {
        try
        {
            // Step 1: Ottieni tutte le terapie attive oggi
            var terapieAttiveOggi = await _context.Terapie
                .Where(t => t.DataInizio <= oggi && t.DataFine >= oggi)
                .ToListAsync();

            if (!terapieAttiveOggi.Any())
                return NotFound("Nessuna terapia da somministrare oggi.");

            // Step 2: Ottieni ID dei pazienti coinvolti
            var pazienteIds = terapieAttiveOggi.Select(t => t.IDPaziente).Distinct().ToList();

            // Step 3: Carica solo i pazienti del reparto richiesto
            var pazientiDelReparto = await _context.Pazienti
                .Where(p => pazienteIds.Contains(p.ID) && p.IDReparto == IDReparto)
                .ToListAsync();

            var pazienteIdsNelReparto = pazientiDelReparto.Select(p => p.ID).ToHashSet();

            // Step 4: Filtra le terapie i cui pazienti sono nel reparto indicato
            var terapieFiltrate = terapieAttiveOggi
                .Where(t => pazienteIdsNelReparto.Contains(t.IDPaziente))
                .ToList();

            if (!terapieFiltrate.Any())
                return NotFound("Nessuna terapia da somministrare oggi per il reparto specificato.");

            return Ok(terapieFiltrate);
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

    public class DatiTerapia
    {
        public string? Farmaco { get; set; }
        public string? Dosaggio { get; set; }
        public TimeOnly? OrarioSomministrazione { get; set; }
        public DateOnly? DataInizio { get; set; }
        public DateOnly? DataFine { get; set; }
    }

}
