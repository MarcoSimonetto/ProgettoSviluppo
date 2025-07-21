using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;
using System.Collections.Generic;
using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Net;

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

        [HttpGet]
        public async Task<IActionResult> RichiestaTrasferimentoPaziente()
        {
            var repartoId = HttpContext.Session.GetInt32("Reparto");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (repartoId == null || matricola == null || password == null)
            {
                return RedirectToAction("Login", "Utenti");
            }

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            // Ottieni pazienti nel reparto dell’utente loggato
            var pazientiResponse = await _client.GetAsync($"api/pazienti/reparto/{repartoId}");
            var repartiResponse = await _client.GetAsync("api/reparti");

            if (!pazientiResponse.IsSuccessStatusCode || !repartiResponse.IsSuccessStatusCode)
            {
                TempData["Error"] = "Errore nel caricamento dei dati per il trasferimento.";
                return RedirectToAction("Index", "Home");
            }

            var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
            var repartiJson = await repartiResponse.Content.ReadAsStringAsync();

            var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(pazientiJson);
            var reparti = JsonConvert.DeserializeObject<List<Reparto>>(repartiJson);

            ViewBag.Pazienti = pazienti;
            ViewBag.Reparti = reparti;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RichiestaTrasferimentoPaziente(int idPaziente, int idRepartoDestinazione, int numeroLetto)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var payload = new
            {
                IDReparto = idRepartoDestinazione,
                NumeroLetto = numeroLetto
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PutAsync($"api/pazienti/trasferimento/{idPaziente}", content);

            if (response.IsSuccessStatusCode)
                TempData["Success"] = "Trasferimento completato con successo.";
            else
                TempData["Error"] = "Errore nel trasferimento: " + await response.Content.ReadAsStringAsync();

            return RedirectToAction(nameof(RichiestaTrasferimentoPaziente));
        }

        [HttpGet]
        public IActionResult Prenota()
        {
            return View(new Paziente());
        }

        [HttpPost]
        public async Task<IActionResult> Prenota(Paziente paziente)
        {
            int? matricola = HttpContext.Session.GetInt32("Matricola");
            int? idReparto = HttpContext.Session.GetInt32("Reparto");
            string? password = HttpContext.Session.GetString("Password");

            if (matricola == null || idReparto == null || string.IsNullOrEmpty(password))
            {
                TempData["LoginError"] = "Sessione scaduta. Effettuare il login.";
                return RedirectToAction("Login", "Utenti");
            }

            paziente.IDReparto = idReparto.Value;

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var jsonContent = JsonConvert.SerializeObject(paziente);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("api/pazienti/aggiungi", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Paziente prenotato con successo.";
                return RedirectToAction("Pazienti"); // torna alla lista dei pazienti
            }

            var error = await response.Content.ReadAsStringAsync();
            ModelState.AddModelError(string.Empty, $"Errore: {error}");
            return View(paziente);
        }

        [HttpGet]
        public async Task<IActionResult> RicoveroUrgente(int idPaziente)
        {
            var idReparto = HttpContext.Session.GetInt32("Reparto");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (idReparto == null || matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.GetAsync($"/api/reparti/lista_letti_liberi/{idReparto}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["LogError"] = "Errore nel recupero letti.";
                var errore = await response.Content.ReadAsStringAsync();
                _logger.LogError("Errore nel recupero letti: {Status} - {Msg}", response.StatusCode, errore);
                return RedirectToAction("Pazienti");
            }

            var content = await response.Content.ReadAsStringAsync();
            var lettiLiberi = JsonConvert.DeserializeObject<List<int>>(content);
            ViewBag.LettiLiberi = lettiLiberi;

            return View("RicoveroUrgente", new Paziente
            {
                ID = idPaziente,
                IDReparto = idReparto.Value
            });
        }

        [HttpPost]
        public async Task<IActionResult> RicoveroUrgente(Paziente paziente)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            paziente.DataRicovero = DateOnly.FromDateTime(DateTime.Today);

            // Serializza tutto il modello Paziente
            var json = JsonConvert.SerializeObject(paziente);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("api/pazienti/aggiungi", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Paziente ricoverato con successo.";
                return RedirectToAction("Pazienti");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Errore nel ricovero urgente: {0}", errorContent);
                ModelState.AddModelError(string.Empty, "Errore durante il ricovero: " + errorContent);
                return View(paziente);
            }
        }

        [HttpGet]
        public async Task<IActionResult> DaRicoverareOggi()
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            // Chiamata per pazienti da ricoverare
            var pazientiResponse = await _client.GetAsync($"api/pazienti/da_ricoverare/{idReparto}/oggi");

            List<Paziente> pazienti = new List<Paziente>();

            if (pazientiResponse.IsSuccessStatusCode)
            {
                var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
                pazienti = JsonConvert.DeserializeObject<List<Paziente>>(pazientiJson);
            }
            else if (pazientiResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                // Mostra errore solo se NON è un 404
                TempData["Error"] = "Errore nel recupero dei pazienti.";
            }

            // Chiamata per letti liberi
            var lettiResponse = await _client.GetAsync($"api/reparti/lista_letti_liberi/{idReparto}");

            List<int> lettiLiberi = new List<int>();

            if (lettiResponse.IsSuccessStatusCode)
            {
                var lettiJson = await lettiResponse.Content.ReadAsStringAsync();
                lettiLiberi = JsonConvert.DeserializeObject<List<int>>(lettiJson);
            }
            else
            {
                TempData["Error"] = "Errore nel recupero dei letti liberi.";
            }

            ViewBag.LettiLiberi = lettiLiberi;
            return View(pazienti);
        }


        [HttpPost]
        public async Task<IActionResult> DaRicoverareOggi(int ID, int NumeroLetto)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.PutAsync($"api/pazienti/ricovera/{ID}/{NumeroLetto}", null);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Paziente ricoverato.";
                return RedirectToAction("DaRicoverareOggi");
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Errore nel ricovero urgente: {0}", error);
            TempData["Error"] = "Errore nel ricovero: " + error;

            return RedirectToAction("DaRicoverareOggi");
        }


        [HttpGet]
        public async Task<IActionResult> DaDimettere()
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.GetAsync($"api/pazienti/da_dimettere/{idReparto}/oggi");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Caso previsto: nessun paziente da dimettere oggi
                TempData["Info"] = "Nessun paziente da dimettere oggi.";
                return View(new List<Paziente>());
            }

            if (!response.IsSuccessStatusCode)
            {
                // Altri errori (es. 500, 400...)
                _logger.LogWarning("Errore nel recupero pazienti da dimettere: {0}", response.StatusCode);
                TempData["Error"] = "Errore nel recupero dei pazienti.";
                return View(new List<Paziente>());
            }

            var json = await response.Content.ReadAsStringAsync();

            List<Paziente> pazienti;
            try
            {
                pazienti = JsonConvert.DeserializeObject<List<Paziente>>(json) ?? new List<Paziente>();
            }
            catch (JsonException ex)
            {
                _logger.LogError("Errore durante la deserializzazione JSON: {0}", ex.Message);
                TempData["Error"] = "Errore nella lettura dei dati.";
                return View(new List<Paziente>());
            }

            return View(pazienti);
        }


        [HttpPost]
        public async Task<IActionResult> DaDimettere(int ID)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.DeleteAsync($"api/pazienti/dimetti/{ID}");

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Paziente dimesso con successo.";
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Errore nella dimissione: {0}", errorContent);
                TempData["Error"] = "Errore nella dimissione: " + errorContent;
            }

            return RedirectToAction("DaDimettere");
        }

        [HttpGet]
        public async Task<IActionResult> ModificaDatiMedici(int id)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.GetAsync($"api/pazienti/{id}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Errore nel caricamento del paziente.";
                return RedirectToAction("Index");
            }

            var json = await response.Content.ReadAsStringAsync();
            var paziente = JsonConvert.DeserializeObject<Paziente>(json);

            return View(paziente);
        }


        [HttpPost]
        public async Task<IActionResult> InviaModificaDatiMedici(int IDPaziente, DateOnly? DataRicovero, DateOnly? DataDimissione, string? MotivoRicovero, string? Patologie, string? Allergie, string? AltreNote)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var dati = new
            {
                DataRicovero,
                DataDimissione,
                MotivoRicovero,
                Patologie,
                Allergie,
                AltreNote
            };

            var content = new StringContent(JsonConvert.SerializeObject(dati), Encoding.UTF8, "application/json");

            var response = await _client.PutAsync($"api/pazienti/modifica_dati_medici/{IDPaziente}", content);

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Errore nella modifica dei dati: " + await response.Content.ReadAsStringAsync();
            }
            else
            {
                TempData["Success"] = "Modifica avvenuta con successo.";
            }

            return RedirectToAction("Pazienti");
        }

        [HttpGet]
        public async Task<IActionResult> ModificaDatiPersonali(int id)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.GetAsync($"api/pazienti/{id}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Errore nella modifica dei dati personali: " + await response.Content.ReadAsStringAsync();
                return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
            }

            var json = await response.Content.ReadAsStringAsync();
            var paziente = JsonConvert.DeserializeObject<Paziente>(json);

            return View(paziente);
        }



        [HttpPost]
        public async Task<IActionResult> ModificaDatiPersonali(int IDPaziente, string? CF, string? Nome, string? Cognome, string? LuogoNascita, DateOnly? DataNascita)
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var dati = new
            {
                CF,
                Nome,
                Cognome,
                LuogoNascita,
                DataNascita
            };

            var content = new StringContent(JsonConvert.SerializeObject(dati), Encoding.UTF8, "application/json");

            var response = await _client.PutAsync($"api/pazienti/modifica/{IDPaziente}", content);

            if (!response.IsSuccessStatusCode)
            {
                TempData["ServerMessage"] = "Errore nella modifica dei dati personali " + await response.Content.ReadAsStringAsync();
                return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
            }
            else
            {
                TempData["Success"] = "Modifica avvenuta con successo.";
            }

            return RedirectToAction("Pazienti");
        }
        [HttpGet]
        public async Task<IActionResult> ProssimiRicoveri()
        {
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (matricola == null || password == null)
                return RedirectToAction("Login", "Utenti");

            string authString = $"{matricola}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var response = await _client.GetAsync($"api/pazienti/da_ricoverare/{idReparto}");

            if (!response.IsSuccessStatusCode)
            {
                TempData["Error"] = "Errore nel caricamento dei pazienti da ricoverare.";
                return View(new List<Paziente>());
            }

            var json = await response.Content.ReadAsStringAsync();
            var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(json);

            return View(pazienti);
        }

 

    }
}
