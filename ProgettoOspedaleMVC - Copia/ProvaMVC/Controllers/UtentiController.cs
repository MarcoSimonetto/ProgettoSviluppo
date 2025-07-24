using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using ProvaMVC.Models;
using System.Net.Sockets;

namespace ProvaMVC.Controllers;

public class UtentiController : Controller
{
    private readonly HttpClient Client;


    public UtentiController(HttpClient client)
    {
        Client = client;
        
        Client.BaseAddress = new Uri("http://localhost:5002");
    }

    [HttpGet]
    public IActionResult Login()
    {
        try { 
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

    [HttpPost]
    public async Task<IActionResult> Login(LoginData utente)
    {
        try { 

        var matricola = HttpContext.Session.GetInt32("Matricola");
        var json = JsonConvert.SerializeObject(utente);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("api/utenti/login", content);
            if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                var errore = await response.Content.ReadAsStringAsync();
                TempData["LogError"] = $"Errore durante il login: {errore}";
                return RedirectToAction("HttpError", "Home", new { statusCode = 401 }); ///pass o matrciola sbagliati
            }
            else if (response.IsSuccessStatusCode )
            {
                var result = await response.Content.ReadAsStringAsync();
                var utenteLoggato = JsonConvert.DeserializeObject<UtenteLoggato>(result);
                HttpContext.Session.SetInt32("Matricola", utenteLoggato.Mat);
                HttpContext.Session.SetString("Ruolo", utenteLoggato.RuoloMat);
                HttpContext.Session.SetInt32("Reparto", utenteLoggato.Reparto);
                HttpContext.Session.SetString("Password", utenteLoggato.Pass);
                return RedirectToAction("Index", "Home");
                
            }
            else
            { 
                var errore = await response.Content.ReadAsStringAsync();
                TempData["LogError"] = $"Errore durante il login: {errore}";
                return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode }); ///server spento
            }
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

    [HttpGet]
    public async Task<IActionResult> Registrazione()
    {
        try { 
        var model = new RegistrazioneData();

        var response = await Client.GetAsync("api/reparti");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var reparti = JsonConvert.DeserializeObject<List<Reparto>>(json);
            model.Reparti = reparti;
        }

        return View(model);
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

    [HttpPost]
    public async Task<IActionResult> Registrazione(RegistrazioneData model, Utente nuovoUtente)
    {

        try { 
        var jsonBody = JsonConvert.SerializeObject(nuovoUtente);
        Console.WriteLine($"RepartoID ricevuto: {nuovoUtente.IDReparto}");
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("api/utenti/registrazione", content);
        if (!response.IsSuccessStatusCode)
        {
                TempData["ServerMessage"] = "Errore nella modifica dei dati personali " + await response.Content.ReadAsStringAsync();
                return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
            }
            else
            {
                TempData["SuccessMessage"] = "Registrazione avvenuta con successo! Effettua il login con matricola."+ await response.Content.ReadAsStringAsync(); 
                return RedirectToAction("Registrazione");
            }
                
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
    [HttpPost]
    public IActionResult Logout()
    {
        try { 
        HttpContext.Session.Remove("Matricola");
        HttpContext.Session.Remove("Ruolo");
        HttpContext.Session.Remove("Reparto");
        HttpContext.Session.Remove("Password");

        HttpContext.Session.Clear();

        return RedirectToAction("Login");
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
