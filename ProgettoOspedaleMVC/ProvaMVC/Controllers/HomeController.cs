using System.Diagnostics;
using Humanizer.Localisation;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;

namespace ProvaMVC.Controllers;

public class HomeController : Controller
{
    private readonly HttpClient Client;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger, HttpClient client)
    {
        _logger = logger;
        Client = client;
        //Client.BaseAddress = new Uri("http://localhost:5099");
        Client.BaseAddress = new Uri("http://localhost:5002");
    }

    public async Task<IActionResult> Index() ///cerca la view in una cartella Home (nome del controller) o Shared
    {
        var reparto = HttpContext.Session.GetInt32("Reparto");
        var ruolo = HttpContext.Session.GetString("Ruolo");
        var matricola = HttpContext.Session.GetInt32("Matricola");
        var password = HttpContext.Session.GetString("Password");

        if (matricola == null || ruolo == null || reparto == null)
        {
            return RedirectToAction("Login", "Utenti");
        }

        ViewBag.Reparto = reparto;
        ViewBag.Ruolo = ruolo;
        ViewBag.Matricola = matricola;

        
        ///parte nuova MODIFICA QUESTA  PARTE IN BASE CHE IO ABBIA LA GET ap/pazienti/reparto/id reparto( che è nella variabile reparto )

        try
        {

            string authString = $"{matricola}:{password}";
            string base64Token = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

            // Clear any existing authorization headers to avoid conflicts
            Client.DefaultRequestHeaders.Authorization = null;
            Client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);


            var response = await Client.GetAsync($"api/pazienti/reparto/{reparto}");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();

                var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(result)!;

                ViewBag.Pazienti = pazienti;

                return View();
            }
            else
            {
                // Read the error content from the API response
                var errore = await response.Content.ReadAsStringAsync();
                _logger.LogError("API returned an error: {StatusCode} - {ErrorMessage}", response.StatusCode, errore);
                TempData["LogError"] = $"Errore durante il recupero dei pazienti: {response.StatusCode} - {errore}";

                // Optionally, if it's an Unauthorized (401) response, redirect to login
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden) // Include Forbidden for authorization issues
                {
                    TempData["LoginError"] = "Access unauthorized or forbidden. Please log in again.";
                    return RedirectToAction("Login", "Utenti");
                }

                return View();
            }
        }
        catch (HttpRequestException ex)
        {
            // Handle network errors or API not reachable
            _logger.LogError(ex, "Network error or API not reachable during patient retrieval.");
            TempData["LogError"] = "Impossibile connettersi al server. Si prega di riprovare più tardi.";
            return View();
        }
        catch (JsonSerializationException ex)
        {
            // Handle JSON deserialization errors
            _logger.LogError(ex, "Errore durante la deserializzazione JSON dei dati dei pazienti.");
            TempData["LogError"] = "Errore nella lettura dei dati dei pazienti dal server.";
            return View();
        }
        catch (Exception ex)
        {
            // Catch any other unexpected errors
            _logger.LogError(ex, "Errore imprevisto durante la chiamata al server per ottenere i pazienti.");
            TempData["LogError"] = "Errore imprevisto durante il recupero dei pazienti.";
            return View();
        }
        return View();
    }

    public IActionResult Reparto()
    {
        return View();
    }

    public IActionResult Pazienti()
    {
        return View();
    }

    public IActionResult Terapie()
    {
        var ruolo = HttpContext.Session.GetString("Ruolo");
        if (ruolo == "Medico" || ruolo == "Infermieri")
        {
            return View();
        }
        else
        {
            TempData["AccessDenied"] = "Non si dispone delle autorizzazioni necessarie per accedere a questa sezione.";
            return RedirectToAction("Index");
        }
    }

    public IActionResult AlertTerapieScadenza()
    {
        var ruolo = HttpContext.Session.GetString("Ruolo");
        if (ruolo == "Infermieri")
        {
            return View();
        }
        else
        {
            TempData["AccessDenied"] = "Funzionalità riservata agli infermieri.";
            return RedirectToAction("Index");
        }
    }

    public IActionResult CalcoloLettiDisponibili()
    {
        return View();
    }

    public IActionResult RichiestaTrasferimentoPaziente()
    {
        return View();
    }

    public IActionResult ModificaDati()
    {
        var ruolo = HttpContext.Session.GetString("Ruolo");
        if (ruolo == "Medico")
        {
            ViewBag.CanEditMedicoData = true;
        }
        else
        {
            ViewBag.CanEditMedicoData = false;
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
