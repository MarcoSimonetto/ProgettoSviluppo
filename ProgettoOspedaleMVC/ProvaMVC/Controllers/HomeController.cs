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

    public IActionResult RichiestaTrasferimentoPaziente()
    {
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
    public IActionResult HttpError(int statusCode)
    {
        ViewData["ServerMessage"] = TempData["ServerMessage"];
        return View(statusCode);
            
    }

}
