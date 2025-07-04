using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProvaMVC.Models;

namespace ProvaMVC.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index() ///cerca la view in una cartella Home (nome del controller) o Shared
    {
        var reparto = HttpContext.Session.GetInt32("Reparto");
        var ruolo = HttpContext.Session.GetString("Ruolo");
        var matricola = HttpContext.Session.GetInt32("Matricola");

        if (matricola == null || ruolo == null || reparto == null)
        {
            return RedirectToAction("Login", "Utenti");
        }

        ViewBag.Reparto = reparto;
        ViewBag.Ruolo = ruolo;
        ViewBag.Matricola = matricola;

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
