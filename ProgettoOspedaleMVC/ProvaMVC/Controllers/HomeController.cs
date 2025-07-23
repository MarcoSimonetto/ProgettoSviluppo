using System.Diagnostics;
using Humanizer.Localisation;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;
using System.Net.Sockets;

namespace ProvaMVC.Controllers;

public class HomeController : Controller
{
    private readonly HttpClient Client;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger, HttpClient client)
    {
        _logger = logger;
        Client = client;
        Client.BaseAddress = new Uri("http://localhost:5002");
    }

    public async Task<IActionResult> Index() ///cerca la view in una cartella Home (nome del controller) o Shared
    {
        try { 
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



    public IActionResult Privacy()
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        try { 
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
            TempData["ServerMessage"] = "Errore imprevisto: " + ex.Message;
            return RedirectToAction("HttpError", "Home", new { statusCode = 500 });
        }
    }
    public IActionResult HttpError(int statusCode)
    {
        try { 
        //ViewData["ServerMessage"] = TempData["ServerMessage"];
        return View(statusCode);
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
