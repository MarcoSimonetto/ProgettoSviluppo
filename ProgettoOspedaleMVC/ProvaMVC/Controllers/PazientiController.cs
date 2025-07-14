using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;
using System.Collections.Generic;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ProvaMVC.Controllers
{
    public class PazientiController : Controller
    {
        private readonly HttpClient _client;
        private readonly ILogger<PazientiController> _logger;

        public PazientiController(IHttpClientFactory clientFactory, ILogger<PazientiController> logger)
        {
            _client = clientFactory.CreateClient();
            _client.BaseAddress = new Uri("http://localhost:5002");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Pazienti()
        {
            // Recupera matricola e password dalla sessione
            int? matricolaInt = HttpContext.Session.GetInt32("Matricola");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (matricolaInt == null)
            {
                TempData["LoginError"] = "Sessione scaduta o credenziali mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            string matricola = matricolaInt.Value.ToString();

            string? password = HttpContext.Session.GetString("Password");

            if (string.IsNullOrEmpty(matricola) || string.IsNullOrEmpty(password))
            {
                TempData["LoginError"] = "Sessione scaduta o credenziali mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            try
            {
                _logger.LogInformation("Matricola from session: {Matricola}", matricola);
                _logger.LogInformation("Password from session: {Password}", password); // Usa LogDebug in produzione

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

                _client.DefaultRequestHeaders.Authorization = null;
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var response = await _client.GetAsync($"api/pazienti/reparto/{idReparto}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(result)!;

                    ViewBag.Pazienti = pazienti;
                    return View("Pazienti");
                }
                else
                {
                    var errore = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API returned an error: {StatusCode} - {ErrorMessage}", response.StatusCode, errore);
                    TempData["LogError"] = $"Errore durante il recupero dei pazienti: {response.StatusCode} - {errore}";

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        TempData["LoginError"] = "Accesso non autorizzato. Effettuare nuovamente il login.";
                        return RedirectToAction("Login", "Utenti");
                    }

                    return View("Pazienti");
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Errore di rete o API non raggiungibile durante il recupero dei pazienti.");
                TempData["LogError"] = "Impossibile connettersi al server. Riprovare più tardi.";
                return View("Pazienti");
            }
            catch (JsonSerializationException ex)
            {
                _logger.LogError(ex, "Errore nella deserializzazione dei dati JSON dei pazienti.");
                TempData["LogError"] = "Errore nella lettura dei dati dei pazienti.";
                return View("Pazienti");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore imprevisto durante la richiesta dei pazienti.");
                TempData["LogError"] = "Errore imprevisto durante il recupero dei pazienti.";
                return View("Pazienti");
            }
        }
    }
}
