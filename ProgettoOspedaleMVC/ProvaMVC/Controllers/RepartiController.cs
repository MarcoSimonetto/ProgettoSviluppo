using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;

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

        try
        {
            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);

            var response = await _client.GetAsync($"api/reparti/{repartoId}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var reparto = JsonConvert.DeserializeObject<Reparto>(json)!;

                ViewBag.Reparto = reparto;

                return View();
            }
            else
            {
                var errore = await response.Content.ReadAsStringAsync();
                _logger.LogError("Errore API reparto: {StatusCode} - {Errore}", response.StatusCode, errore);
                TempData["LogError"] = $"Errore nel recupero del reparto: {response.StatusCode} - {errore}";

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TempData["LoginError"] = "Accesso negato. Effettua di nuovo il login.";
                    return RedirectToAction("Login", "Utenti");
                }

                return View();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Errore di rete nel recupero del reparto");
            TempData["LogError"] = "Errore di connessione al server. Riprova più tardi.";
            return View();
        }
        catch (JsonSerializationException ex)
        {
            _logger.LogError(ex, "Errore deserializzazione JSON del reparto");
            TempData["LogError"] = "Errore nella lettura dei dati del reparto.";
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore imprevisto durante il recupero del reparto");
            TempData["LogError"] = "Errore imprevisto durante il recupero del reparto.";
            return View();
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

            var response = await _client.GetAsync($"api/pazienti/reparto/{repartoId}/{numero}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var paziente = JsonConvert.DeserializeObject<Paziente>(json);

                if (paziente != null)
                {
                    return View("VisualizzaLetto", paziente); // View personalizzata
                }
                else
                {
                    TempData["LogInfo"] = "Nessun paziente è attualmente ricoverato in questo letto.";
                    return View("VisualizzaLetto", null); // oppure passare un flag
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                TempData["LogInfo"] = "Letto libero.";
                return View("VisualizzaLetto", null);
            }
            else
            {
                var errore = await response.Content.ReadAsStringAsync();
                _logger.LogError("Errore API letto: {StatusCode} - {Errore}", response.StatusCode, errore);
                TempData["LogError"] = $"Errore nel recupero del letto: {errore}";
                return View("VisualizzaLetto", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante la chiamata a VisualizzaLetto");
            TempData["LogError"] = "Errore durante il recupero del letto.";
            return View("VisualizzaLetto", null);
        }
    }

}