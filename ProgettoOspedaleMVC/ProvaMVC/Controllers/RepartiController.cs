using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace ProvaMVC.Controllers;

public class RepartiController : Controller
{
    private readonly HttpClient _client;

    public RepartiController(HttpClient client)
    {
        _client = client;
        _client.BaseAddress = new Uri("http://localhost:5002");
    }

    //carica la razor reparti
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
        //impostiamo l'ora dell'ultimo aggiornamento
        ViewBag.DataAggiornamento = DateTime.Now;

        try
        {
            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);

            //prendiamo il nome del reparto e il numero di letti 
            var repartoResponse = await _client.GetAsync($"api/reparti/{repartoId}");
            repartoResponse.EnsureSuccessStatusCode();

            var repartoJson = await repartoResponse.Content.ReadAsStringAsync();
            var reparto = JsonConvert.DeserializeObject<ProvaMVC.Models.Reparto>(repartoJson)!;

            //prendiamo i pazienti del reparto
            var pazientiResponse = await _client.GetAsync($"api/pazienti/Reparto/{repartoId}");
            pazientiResponse.EnsureSuccessStatusCode();

            var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
            var pazienti = JsonConvert.DeserializeObject<List<ProvaMVC.Models.Paziente>>(pazientiJson) ?? new List<ProvaMVC.Models.Paziente>();

            int lettiTotali = reparto.NumeroLetti;
            int lettiOccupati = pazienti.Count; //conto il numero di pazienti e sono i letti occupati 
            int lettiDisponibili = lettiTotali - lettiOccupati; //calcolo del numero letti disponibili, avendo i totali - occupati

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

    //metodo che permette di visualizzare il paziente all interno del letto
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

            //torna il paziente che Ã¨ ricoverato nel letto "numero"
            var response = await _client.GetAsync($"api/reparti/{repartoId}/{numero}");

            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var paziente = JsonConvert.DeserializeObject<ProvaMVC.Models.Paziente>(json);

                //se non ce paziente, si puo visualizzare i dati del paziente 
                if (paziente != null)
                    return View("VisualizzaLetto", paziente);

                TempData["LogInfo"] = "Nessun paziente trovato per questo letto.";
            }
            else
            {
                TempData["ServerMessage"] = await response.Content.ReadAsStringAsync();
                return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });

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