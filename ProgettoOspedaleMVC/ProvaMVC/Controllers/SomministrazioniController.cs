using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ProvaMVC.Models;
using System.Net.Http.Headers;
using System.Text;

namespace ProvaMVC.Controllers
{
    public class SomministrazioniController : Controller
    {
        private readonly HttpClient Client;
        private readonly ILogger<SomministrazioniController> _logger;

        public SomministrazioniController(ILogger<SomministrazioniController> logger, HttpClient client)
        {
            _logger = logger;
            Client = client;
            Client.BaseAddress = new Uri("http://localhost:5002"); // URL backend
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        [HttpGet]
        public async Task<IActionResult> Dettaglio(int idTerapia)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (!matricola.HasValue || string.IsNullOrEmpty(password))
            {
                TempData["LoginError"] = "Sessione scaduta. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            try
            {
                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                // Recupero terapia
                var responseTerapia = await Client.GetAsync($"api/terapie/{idTerapia}");
                if (!responseTerapia.IsSuccessStatusCode)
                    return RedirectToAction("Index", "Terapie");

                var jsonTerapia = await responseTerapia.Content.ReadAsStringAsync();
                var terapia = JsonConvert.DeserializeObject<Terapia>(jsonTerapia);

                // Recupero paziente
                var responsePaziente = await Client.GetAsync($"api/pazienti/{terapia.IDPaziente}");
                if (!responsePaziente.IsSuccessStatusCode)
                    return RedirectToAction("Index", "Terapie");

                var jsonPaziente = await responsePaziente.Content.ReadAsStringAsync();
                var paziente = JsonConvert.DeserializeObject<Paziente>(jsonPaziente);

                // Recupero somministrazioni odierne
                var responseSomministrazioni = await Client.GetAsync($"api/somministrazioni/oggi/terapia/{idTerapia}");
                var jsonSomministrazioni = await responseSomministrazioni.Content.ReadAsStringAsync();
                var somministrazioni = JsonConvert.DeserializeObject<List<Somministrazione>>(jsonSomministrazioni) ?? new();

                // Stato e calcolo della differenza oraria precisa
                string stato;
                string tempoRimanenteOltre = string.Empty;

                if (somministrazioni.Any())
                {
                    stato = "Somministrata";
                }
                else
                {
                    var orarioPrevisto = terapia.OrarioSomministrazione.ToTimeSpan();
                    var oraAttuale = DateTime.Now.TimeOfDay;
                    var differenza = orarioPrevisto - oraAttuale; // This is a TimeSpan

                    if (differenza.TotalMinutes > 0)
                    {
                        stato = "In orario";
                        tempoRimanenteOltre = $"Mancano {Math.Floor(differenza.TotalHours)} ore e {differenza.Minutes} minuti";
                    }
                    else
                    {
                        stato = "In ritardo";
                        // Use absolute value for display, as it's a "delay"
                        tempoRimanenteOltre = $"In ritardo di {Math.Floor(Math.Abs(differenza.TotalHours))} ore e {Math.Abs(differenza.Minutes)} minuti";
                    }
                }

                // Verifica via API
                var giaSomministrata = await VerificaSomministrazione(idTerapia, DateOnly.FromDateTime(DateTime.Today));

                ViewBag.Terapia = terapia;
                ViewBag.Paziente = paziente;
                ViewBag.Somministrazioni = somministrazioni;
                ViewBag.GiaSomministrata = giaSomministrata;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel dettaglio somministrazioni");
                TempData["ErrorMessage"] = "Errore nel caricamento dei dettagli.";
                return RedirectToAction("Index", "Terapie");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Somministra(int idTerapia)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (!matricola.HasValue || string.IsNullOrEmpty(password))
            {
                TempData["LoginError"] = "Sessione scaduta. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            try
            {
                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var payload = new
                {
                    id = 0,
                    matricolaUtente = matricola.Value,
                    idTerapia = idTerapia,
                    data = DateTime.Today.ToString("yyyy-MM-dd")
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("api/Somministrazioni/aggiungi", content);

                if (response.IsSuccessStatusCode)
                    TempData["SuccessMessage"] = "Somministrazione registrata con successo!";
                else
                    TempData["ErrorMessage"] = "Errore durante la registrazione.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nella POST di somministrazione");
                TempData["ErrorMessage"] = "Errore imprevisto durante la somministrazione.";
            }

            return RedirectToAction("Index", "Terapie");
        }

        [HttpGet]
        private async Task<bool> VerificaSomministrazione(int idTerapia, DateOnly data)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (!matricola.HasValue || string.IsNullOrEmpty(password))
                return false;

            try
            {
                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var response = await Client.GetAsync($"api/Somministrazioni/verifica/{idTerapia}/{data:yyyy-MM-dd}");
                if (!response.IsSuccessStatusCode) return false;

                var content = await response.Content.ReadAsStringAsync();
                return content.Contains("System.Collections.Generic.List");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la verifica somministrazione");
                return false;
            }
        }

        [HttpGet]
        public async Task<IActionResult> AlertTerapia()
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (!matricola.HasValue || string.IsNullOrEmpty(password) || !idReparto.HasValue)
            {
                TempData["LoginError"] = "Sessione scaduta. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            try
            {
                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var response = await Client.GetAsync($"api/Somministrazioni/oggi/in_ritardo/{idReparto}");
                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Errore nel recupero delle terapie in ritardo.";
                    return View(new List<dynamic>());
                }

                var json = await response.Content.ReadAsStringAsync();
                var terapieInRitardo = JsonConvert.DeserializeObject<List<dynamic>>(json);

                ViewBag.TerapieInRitardo = terapieInRitardo;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel caricamento delle terapie in ritardo");
                TempData["ErrorMessage"] = "Errore interno. Riprova più tardi.";
                return RedirectToAction("Index", "Home");
            }
        }



    }
}