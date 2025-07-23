using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ProvaMVC.Controllers;

public class RepartiController : Controller
{
    private readonly HttpClient _client;
    private readonly ILogger<RepartiController> _logger;

    public RepartiController(ILogger<RepartiController> logger, HttpClient client)
    {
        _logger = logger;
        _client = client;
        _client.BaseAddress = new Uri("http://localhost:5002");
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

        
        ViewBag.Reparto = null;
        ViewBag.Pazienti = new List<ProvaMVC.Models.Paziente>();
        ViewBag.LettiTotali = 0;
        ViewBag.LettiOccupati = 0;
        ViewBag.LettiDisponibili = 0;
        ViewBag.DataAggiornamento = DateTime.Now;

        try
        {
            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);

            var repartoResponse = await _client.GetAsync($"api/reparti/{repartoId}");
            repartoResponse.EnsureSuccessStatusCode();

            var repartoJson = await repartoResponse.Content.ReadAsStringAsync();
            var reparto = JsonConvert.DeserializeObject<ProvaMVC.Models.Reparto>(repartoJson)!; 

            var pazientiResponse = await _client.GetAsync($"api/pazienti/Reparto/{repartoId}");
            pazientiResponse.EnsureSuccessStatusCode();

            var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
            var pazienti = JsonConvert.DeserializeObject<List<ProvaMVC.Models.Paziente>>(pazientiJson) ?? new List<ProvaMVC.Models.Paziente>();

            int lettiTotali = reparto.NumeroLetti;
            int lettiOccupati = pazienti.Count;
            int lettiDisponibili = lettiTotali - lettiOccupati;

            ViewBag.Reparto = reparto;
            ViewBag.Pazienti = pazienti;
            ViewBag.LettiTotali = lettiTotali;
            ViewBag.LettiOccupati = lettiOccupati;
            ViewBag.LettiDisponibili = lettiDisponibili;
            ViewBag.DataAggiornamento = DateTime.Now;

            return View();
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

    [HttpGet]
    public async Task<IActionResult> GetLettiLiberi(int idReparto)
    {
        try { 
        var matricola = HttpContext.Session.GetInt32("Matricola");
        var password = HttpContext.Session.GetString("Password");

        if (matricola == null || password == null)
            return Unauthorized();

        string authString = $"{matricola}:{password}";
        string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

        var response = await _client.GetAsync($"api/reparti/lista_letti_liberi/{idReparto}");
        var result = await response.Content.ReadAsStringAsync();

        return Content(result, "application/json");
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