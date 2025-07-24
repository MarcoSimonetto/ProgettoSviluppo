using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;
using System.Net.Http.Headers;
using System.Net.Sockets;
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
            Client.BaseAddress = new Uri("http://localhost:5002");
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // Metodo GET che mostra il dettaglio di una terapia specifica
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


                var responseTerapia = await Client.GetAsync($"api/terapie/{idTerapia}");
                if (!responseTerapia.IsSuccessStatusCode)
                    return RedirectToAction("Index", "Terapie");

                var jsonTerapia = await responseTerapia.Content.ReadAsStringAsync();
                var terapia = JsonConvert.DeserializeObject<Terapia>(jsonTerapia);


                var responsePaziente = await Client.GetAsync($"api/pazienti/{terapia.IDPaziente}");
                if (!responsePaziente.IsSuccessStatusCode)
                    return RedirectToAction("Index", "Terapie");

                var jsonPaziente = await responsePaziente.Content.ReadAsStringAsync();
                var paziente = JsonConvert.DeserializeObject<Paziente>(jsonPaziente);


                var giaSomministrata = await VerificaSomministrazione(idTerapia, DateOnly.FromDateTime(DateTime.Today));

                ViewBag.Terapia = terapia;
                ViewBag.Paziente = paziente;
                ViewBag.GiaSomministrata = giaSomministrata;

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

        // Metodo POST che registra la somministrazione odierna di una terapia.
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

                //aggiunge la somministrazione
                var response = await Client.PostAsync("api/Somministrazioni/aggiungi", content);

                if (!response.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                return RedirectToAction("Index", "Terapie");
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

        // Metodo privato che verifica se una terapia è già stata somministrata in una determinata data. Restituisce true se è presente, false altrimenti.
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

                //metodo del server che verifica se la terapia è stata somministrata passando una data (passata nel metodo - e forzata nel formato yyyy-mm-dd)
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

        // Chiama il backend per ottenere le terapie non ancora somministrate ma scadute rispetto all’orario previsto.
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

                //recupero le terapie in ritardo di oggi dato l'idreparto 
                var response = await Client.GetAsync($"api/Somministrazioni/oggi/in_ritardo/{idReparto}");
                if (!response.IsSuccessStatusCode)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        TempData["ServerMessage"] = await response.Content.ReadAsStringAsync();
                        return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                    }
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                var json = await response.Content.ReadAsStringAsync();
                var terapieInRitardo = JsonConvert.DeserializeObject<List<dynamic>>(json);

                ViewBag.TerapieInRitardo = terapieInRitardo;

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



    }
}