// Importazione delle librerie necessarie
using System.Diagnostics;                   // Per tracciare ID delle richieste e diagnostica
using System.Text;                          // Per operazioni su stringhe (non utilizzato nel codice mostrato)
using Microsoft.AspNetCore.Mvc;             // Per usare Controller, IActionResult, ecc.
using ProvaMVC.Models;                      // Accesso ai modelli dell’app MVC
using System.Net.Sockets;                   // Gestione eccezioni legate a socket

namespace ProvaMVC.Controllers;

public class HomeController : Controller
{
    // Client HTTP per comunicare con API esterne
    private readonly HttpClient Client;

    // Logger per tracciare errori e informazioni di debug
    private readonly ILogger<HomeController> _logger;

    // Costruttore del controller: riceve logger e client HTTP 
    public HomeController(ILogger<HomeController> logger, HttpClient client)
    {
        _logger = logger;
        Client = client;
        Client.BaseAddress = new Uri("http://localhost:5002"); // Imposta la base URL per le chiamate API
    }

    // Metodo chiamato all’accesso della homepage
    public async Task<IActionResult> Index()
    {
        try
        {
            // Recupera dati utente dalla sessione corrente
            var reparto = HttpContext.Session.GetInt32("Reparto");
            var ruolo = HttpContext.Session.GetString("Ruolo");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            // Se uno dei dati essenziali è assente → reindirizza al login
            if (matricola == null || ruolo == null || reparto == null)
            {
                return RedirectToAction("Login", "Utenti");
            }

            // Passa i dati alla vista tramite ViewBag (accessibili nella View)
            ViewBag.Reparto = reparto;
            ViewBag.Ruolo = ruolo;
            ViewBag.Matricola = matricola;

            return View();  // Ritorna la vista Index
        }
        catch (HttpRequestException)
        {
            // Errore nel contattare un servizio HTTP (es. API backend non raggiungibile)
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (SocketException)
        {
            // Errore di rete più basso livello (es. connessione interrotta)
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (Exception ex)
        {
            // Qualsiasi altro errore non gestito specificamente
            return RedirectToAction("HttpError", "Home", new { statusCode = 500 });
        }
    }

    // Metodo che restituisce la vista Privacy
    public IActionResult Privacy()
    {
        try
        {
            return View();  // Ritorna la view Privacy.cshtml
        }
        catch (HttpRequestException)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (SocketException)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (Exception ex)
        {
            // Salva messaggio di errore nella TempData (accessibile una volta sola)
            TempData["ServerMessage"] = "Errore imprevisto: " + ex.Message;
            return RedirectToAction("HttpError", "Home", new { statusCode = 500 });
        }
    }

    // Metodo per gestire errori generici (non http), restituisce una view con dettagli sull’errore
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        try
        {
            // Restituisce la vista Error con ID richiesta (per tracciabilità nei log)
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        catch (HttpRequestException)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (SocketException)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (Exception ex)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 500 });
        }
    }

    // Metodo che mostra una pagina di errore HTTP in base al codice passato
    public IActionResult HttpError(int statusCode)
    {
        try
        {
            return View(statusCode); // Ritorna la view associata al codice di errore
        }
        catch (HttpRequestException)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (SocketException)
        {
            return RedirectToAction("HttpError", "Home", new { statusCode = 503 });
        }
        catch (Exception ex)
        {
            TempData["ServerMessage"] = "Errore imprevisto: " + ex.Message;
            return RedirectToAction("HttpError", "Home", new { statusCode = 500 });
        }
    }
}