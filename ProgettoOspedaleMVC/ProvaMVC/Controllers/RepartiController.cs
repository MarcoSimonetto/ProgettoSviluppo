// ... existing usings ...
using System.Text; // Ensure this is present
using Microsoft.AspNetCore.Mvc; // Ensure this is present
using Newtonsoft.Json; // Ensure this is present
using ProvaMVC.Models; // Ensure this is present

namespace ProvaMVC.Controllers;

public class RepartiController : Controller
{
    private readonly HttpClient _client;
    private readonly ILogger<RepartiController> _logger;

    public RepartiController(ILogger<RepartiController> logger, HttpClient client)
    {
        _logger = logger;
        _client = client;
        _client.BaseAddress = new Uri("http://localhost:5002"); // URL API
    }

    public async Task<IActionResult> Index()
    {
        var repartoId = HttpContext.Session.GetInt32("Reparto");
        var matricola = HttpContext.Session.GetInt32("Matricola");
        var password = HttpContext.Session.GetString("Password");
        var ruolo = HttpContext.Session.GetString("Ruolo");

        if (repartoId == null || matricola == null || password == null || ruolo == null)
        {
            return RedirectToAction("Login", "Utenti");
        }

        ViewBag.Ruolo = ruolo;
        ViewBag.Matricola = matricola;

        // Initialize ViewBag.Reparto for error handling in the view
        ViewBag.Reparto = null;
        ViewBag.Pazienti = new List<ProvaMVC.Models.Paziente>(); // Ensure Pazienti is initialized
        ViewBag.LettiTotali = 0;
        ViewBag.LettiOccupati = 0;
        ViewBag.LettiDisponibili = 0;
        ViewBag.DataAggiornamento = DateTime.Now;

        try
        {
            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = null; // Clear previous auth headers
            _client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);

            // Reparto
            var repartoResponse = await _client.GetAsync($"api/reparti/{repartoId}");
            repartoResponse.EnsureSuccessStatusCode(); // This will throw if status is not 2xx

            var repartoJson = await repartoResponse.Content.ReadAsStringAsync();
            var reparto = JsonConvert.DeserializeObject<ProvaMVC.Models.Reparto>(repartoJson)!; // Cast to ProvaMVC.Models.Reparto

            // Pazienti
            // *** CORRECTION HERE: Change 'reparto' to 'Reparto' to match API route casing ***
            var pazientiResponse = await _client.GetAsync($"api/pazienti/Reparto/{repartoId}");
            pazientiResponse.EnsureSuccessStatusCode(); // Add this to catch errors here too

            var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
            var pazienti = JsonConvert.DeserializeObject<List<ProvaMVC.Models.Paziente>>(pazientiJson) ?? new List<ProvaMVC.Models.Paziente>();

            // Conteggio letti
            int lettiTotali = reparto.NumeroLetti;
            int lettiOccupati = pazienti.Count;
            int lettiDisponibili = lettiTotali - lettiOccupati;

            // Passaggio dati alla view
            ViewBag.Reparto = reparto;
            ViewBag.Pazienti = pazienti;
            ViewBag.LettiTotali = lettiTotali;
            ViewBag.LettiOccupati = lettiOccupati;
            ViewBag.LettiDisponibili = lettiDisponibili;
            ViewBag.DataAggiornamento = DateTime.Now;

            return View();
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "Errore HTTP durante il caricamento del reparto o dei pazienti: {StatusCode}", httpEx.StatusCode);
            TempData["LogError"] = $"Errore di connessione all'API: {httpEx.Message}. Assicurati che il server API sia in esecuzione.";
            return View(); // Return view with error data
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore generico durante il caricamento del reparto o dei pazienti");
            TempData["LogError"] = "Errore durante il caricamento dei dati del reparto.";
            return View(); // Return view with error data
        }
    }

    // ... other actions like VisualizzaLetto ...
    public async Task<IActionResult> VisualizzaLetto(int numero)
    {
        var repartoId = HttpContext.Session.GetInt32("Reparto");
        var matricola = HttpContext.Session.GetInt32("Matricola");
        var password = HttpContext.Session.GetString("Password");

        if (repartoId == null || matricola == null || password == null)
        {
            return RedirectToAction("Login", "Utenti");
        }

        try
        {
            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);

            // This API call is correct as per your API RepartiController: [HttpGet("{IDReparto}/{NumeroLetto}")]
            var response = await _client.GetAsync($"api/reparti/{repartoId}/{numero}");

            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var paziente = JsonConvert.DeserializeObject<ProvaMVC.Models.Paziente>(json);

                if (paziente != null)
                    return View("VisualizzaLetto", paziente);

                TempData["LogInfo"] = "Nessun paziente trovato per questo letto.";
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // This will catch the "Reparto non trovato" or "Questo letto non è occupato."
                // from your API's GetPazientePerLetto
                if (json.Contains("Reparto non trovato."))
                {
                    TempData["LogError"] = "Reparto non trovato per visualizzare il letto.";
                }
                else
                {
                    TempData["LogInfo"] = "Letto libero.";
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                TempData["LogError"] = $"Errore nel recupero del letto: {json}";
            }
            else
            {
                TempData["LogError"] = $"Errore nel recupero del letto: {response.StatusCode} - {json}";
            }

            return View("VisualizzaLetto", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il recupero del paziente per letto");
            TempData["LogError"] = "Errore durante il recupero del letto.";
            return View("VisualizzaLetto", null);
        }
    }
}