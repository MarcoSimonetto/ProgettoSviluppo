// ProvaMVC.Controllers/TerapieController.cs
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models; // Assicurati che questa riga sia presente
using System.Net.Http.Headers;
using System.Net.Sockets;

namespace ProvaMVC.Controllers
{
    public class TerapieController : Controller
    {
        private readonly HttpClient Client;
        private readonly ILogger<TerapieController> _logger;

        public TerapieController(ILogger<TerapieController> logger, HttpClient client)
        {
            _logger = logger;
            Client = client;
            Client.BaseAddress = new Uri("http://localhost:5002"); // Assicurati che sia l'indirizzo corretto della tua API
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // --- AZIONE GET: Visualizza la pagina Terapie con elenco e form ---
        [HttpGet]
        public async Task<IActionResult> Terapie()
        {
            
            var ruolo = HttpContext.Session.GetString("Ruolo");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");


            // Popola ViewBag per la vista
            ViewBag.RuoloUtente = ruolo;
            // Se la matricola è null, usa 0 come valore predefinito per il campo MatricolaMedico
            ViewBag.MatricolaMedico = matricola ?? 0;

            List<Terapia> terapie = new List<Terapia>();
            List<Paziente> pazientiDisponibili = new List<Paziente>();
            Dictionary<int, string> pazientiMapPerTabella = new Dictionary<int, string>();

            try
            {
                // Solo se matricola e password non sono null per l'autenticazione Basic
                if (matricola.HasValue && !string.IsNullOrEmpty(password))
                {
                    string authString = $"{matricola}:{password}";
                    string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                    Client.DefaultRequestHeaders.Authorization = null; // Pulisce header precedenti
                    Client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token); // Aggiunge l'autenticazione Basic
                }
                else
                {
                    _logger.LogWarning("Matricola o Password sessione non presenti per autenticazione API.");
                    TempData["ErrorMessage"] = "Errore di autenticazione API. Effettuare nuovamente il login.";
                    return RedirectToAction("Login", "Utenti");
                }


                // 1. Recupera i pazienti del reparto specifico per dropdown e mappa visualizzazione
                var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                if (responsePazienti.IsSuccessStatusCode)
                {
                    var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    pazientiDisponibili = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                    // MODIFICATO QUI: Usa p.CF
                    pazientiMapPerTabella = pazientiDisponibili.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");

                    ViewBag.PazientiDisponibili = pazientiDisponibili; // Per la dropdown nel form
                    ViewBag.PazientiMapPerTabella = pazientiMapPerTabella; // Per visualizzare nella tabella
                }
                else
                {
                    var errorPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    _logger.LogError("Errore API nel recupero pazienti per Terapie (GET): {StatusCode} - {Error}", responsePazienti.StatusCode, errorPazienti);
                    TempData["ErrorMessage"] = $"Impossibile caricare i pazienti del reparto: {errorPazienti}";
                }
                
                // 2. Recupera le terapie (e filtra per i pazienti del reparto)
                var responseTerapie = await Client.GetAsync($"api/Terapie/reparto/{idReparto}");
                if (responseTerapie.IsSuccessStatusCode)
                {
                    var allTerapie = await responseTerapie.Content.ReadAsStringAsync();
                    var deserializedTerapie = JsonConvert.DeserializeObject<List<Terapia>>(allTerapie) ?? new List<Terapia>();

                    // Filtra le terapie in base ai pazienti del reparto recuperati
                    if (pazientiMapPerTabella.Any())
                    {
                        terapie = deserializedTerapie.Where(t => pazientiMapPerTabella.ContainsKey(t.IDPaziente)).ToList();
                    }
                    else
                    {
                        terapie = new List<Terapia>(); // Nessun paziente o nessuna terapia
                    }
                }
                else
                {
                    var errorTerapie = await responseTerapie.Content.ReadAsStringAsync();
                    _logger.LogError("Errore API nel recupero terapie per Terapie (GET): {StatusCode} - {Error}", responseTerapie.StatusCode, errorTerapie);
                    TempData["ErrorMessage"] = $"Impossibile caricare le terapie: {errorTerapie}";
                }
            
                 // La vista riceverà una List<Terapia> come modello
                return View(terapie);
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



        public async Task<IActionResult> Index()
        {
            try {
            return await Terapie();
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

        // GET: Mostra form per nuova terapia (solo Medico)
        [HttpGet]
        public async Task<IActionResult> Assegna()
        {
            try { 

            var ruolo = HttpContext.Session.GetString("Ruolo");

            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var reparto = HttpContext.Session.GetInt32("Reparto");

            // Check for null session values before passing to helper
            if (!matricola.HasValue || string.IsNullOrEmpty(password) || !reparto.HasValue)
            {
                TempData["LoginError"] = "Sessione scaduta o dati utente mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            string authString = $"{matricola.Value}:{password}";
            string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

            var check = await Client.GetAsync("api/utenti/check_ruolo_medico");
            if (!check.IsSuccessStatusCode)
            {
                return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
            }

            await RepopulateViewBagData(matricola.Value, password, reparto.Value, ruolo);
            return View(new Terapia());
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

        // POST: Salva terapia
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assegna(Terapia nuovaTerapia)
        {
            
            var ruolo = HttpContext.Session.GetString("Ruolo");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            // Ensure session data is available
            if (!matricola.HasValue || string.IsNullOrEmpty(password) || !idReparto.HasValue)
            {
                TempData["LoginError"] = "Sessione scaduta o dati utente mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            // Imposta la MatricolaMedico dalla sessione per sicurezza, ignorando quella che potrebbe arrivare dal form
            nuovaTerapia.MatricolaMedico = matricola.Value;

            if (!ModelState.IsValid)
            {
                // If validation fails, repopulate ViewBag data and return the *same* view with the invalid model
                await RepopulateViewBagData(matricola.Value, password, idReparto.Value, ruolo);
                TempData["ErrorMessage"] = "Errore di validazione nel form di assegnazione terapia. Controlla i campi.";
                return View(nuovaTerapia); // Return the view with the current invalid model
            }

            try
            {
                string authString = $"{matricola.Value}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var jsonContent = JsonConvert.SerializeObject(nuovaTerapia);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await Client.PostAsync("api/terapie/assegna", httpContent);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Index"); // Redirect to the main Terapie list
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Errore API nell'assegnazione terapia (POST): {StatusCode} - {Error}", response.StatusCode, errorContent);
                    TempData["ErrorMessage"] = $"Errore durante l'assegnazione della terapia: {response.StatusCode} - {errorContent}";

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        TempData["LoginError"] = "Sessione API scaduta o non autorizzata. Effettuare nuovamente il login.";
                        return RedirectToAction("Login", "Utenti");
                    }
                    // If API returns an error, repopulate ViewBag data and return the *same* view with the invalid model
                    await RepopulateViewBagData(matricola.Value, password, idReparto.Value, ruolo);
                    return View(nuovaTerapia); // Return the view with the current invalid model
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


        // --- Metodo Helper per ripopolare i dati di ViewBag (pazienti, ecc.) ---
        private async Task RepopulateViewBagData(int matricola, string? password, int idReparto, string? ruolo)
        {
           
            try
            {
                // Solo se password non è null
                if (!string.IsNullOrEmpty(password))
                {
                    string authString = $"{matricola}:{password}";
                    string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                    Client.DefaultRequestHeaders.Authorization = null;
                    Client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);
                }
                else
                {
                    _logger.LogWarning("Password sessione non presente per autenticazione API nel ripopolamento ViewBag.");
                }

                // Ricarica pazienti per la dropdown
                var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                if (responsePazienti.IsSuccessStatusCode)
                {
                    var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    var pazientiList = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                    ViewBag.PazientiDisponibili = pazientiList;
                    // MODIFICATO QUI: Usa p.CF
                    ViewBag.PazientiMapPerTabella = pazientiList.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");
                }
                else
                {
                    var errorContent = await responsePazienti.Content.ReadAsStringAsync();
                    _logger.LogError("Errore nel ripopolamento pazienti (helper): {StatusCode} - {Error}", responsePazienti.StatusCode, errorContent);
                    ViewBag.PazientiDisponibili = new List<Paziente>(); // Assicurati che sia vuoto ma non null
                    ViewBag.PazientiMapPerTabella = new Dictionary<int, string>(); // Assicurati che sia vuoto ma non null
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eccezione durante il ripopolamento dei dati ViewBag.");
                ViewBag.PazientiDisponibili = new List<Paziente>();
                ViewBag.PazientiMapPerTabella = new Dictionary<int, string>();
            }

        }

        // --- Metodo Helper per ottenere le terapie esistenti (per la tabella) ---
        private async Task<List<Terapia>> GetExistingTerapie(int matricola, string? password, int idReparto)
        {

            List<Terapia> terapie = new List<Terapia>();
            Dictionary<int, string> pazientiMapPerTabella = new Dictionary<int, string>();

            try
            {
                // Solo se password non è null
                if (!string.IsNullOrEmpty(password))
                {
                    string authString = $"{matricola}:{password}";
                    string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                    Client.DefaultRequestHeaders.Authorization = null;
                    Client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token);
                }
                else
                {
                    _logger.LogWarning("Password sessione non presente per autenticazione API nel recupero terapie esistenti.");
                }

                // Recupera pazienti per la mappa (necessari per filtrare le terapie)
                var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                if (responsePazienti.IsSuccessStatusCode)
                {
                    var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    var pazientiList = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                    // MODIFICATO QUI: Usa p.CF
                    pazientiMapPerTabella = pazientiList.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");
                }
                else
                {
                    _logger.LogError("Errore nel recupero pazienti per GetExistingTerapie (helper).");
                }

                // Recupera le terapie
                var responseTerapie = await Client.GetAsync("api/terapie");
                if (responseTerapie.IsSuccessStatusCode)
                {
                    var allTerapie = await responseTerapie.Content.ReadAsStringAsync();
                    var deserializedTerapie = JsonConvert.DeserializeObject<List<Terapia>>(allTerapie) ?? new List<Terapia>();

                    if (pazientiMapPerTabella.Any())
                    {
                        terapie = deserializedTerapie.Where(t => pazientiMapPerTabella.ContainsKey(t.IDPaziente)).ToList();
                    }
                    else
                    {
                        terapie = new List<Terapia>();
                    }
                }
                else
                {
                    _logger.LogError("Errore nel recupero terapie per GetExistingTerapie (helper).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante il recupero delle terapie esistenti.");
                terapie = new List<Terapia>();
            }
            return terapie;
        }

        // --- MODIFICA TERAPIA ----
        // GET: Mostra form per modifica terapia (solo Medico)
        [HttpGet]
        public async Task<IActionResult> Modifica(int id)
        {
            try { 
            var ruolo = HttpContext.Session.GetString("Ruolo");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            if (ruolo == null || !matricola.HasValue || string.IsNullOrEmpty(password))
                return RedirectToAction("Login", "Utenti");

            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{matricola}:{password}"));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

            var check = await Client.GetAsync("api/utenti/check_ruolo_medico");
            if (!check.IsSuccessStatusCode)
            {
               return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
            }

            var response = await Client.GetAsync($"api/terapie/{id}");
            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Errore nel caricamento della terapia.";
                return RedirectToAction("Index");
            }

            var json = await response.Content.ReadAsStringAsync();
            var terapia = JsonConvert.DeserializeObject<Terapia>(json);
            return View("Modifica", terapia);
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



        // POST: Salva modifica terapia
        [HttpPost]
        public async Task<IActionResult> Modifica(int id, Terapia terapia)
        {
            try { 
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");

            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{matricola}:{password}"));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

                var json = JsonConvert.SerializeObject(new
            {
                farmaco = terapia.Farmaco,
                dosaggio = terapia.Dosaggio,
                orarioSomministrazione = terapia.OrarioSomministrazione.ToString(@"hh\:mm"),
                dataInizio = terapia.DataInizio,
                dataFine = terapia.DataFine
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Client.PutAsync($"api/terapie/modifica/{id}", content);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction("Index");
            }

            TempData["ErrorMessage"] = "Errore nella modifica della terapia.";
            return View("Modifica", terapia);
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


        /// -- ELIMINIA TERAPIA --
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Elimina(int id)
        {
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (!matricola.HasValue || string.IsNullOrEmpty(password))
                {
                    TempData["LoginError"] = "Sessione scaduta.";
                    return RedirectToAction("Login", "Utenti");
                }

                try
                {
                    string authString = $"{matricola}:{password}";
                    string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                    Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                    var check = await Client.GetAsync("api/utenti/check_ruolo_medico");
                    if (!check.IsSuccessStatusCode)
                    {
                        return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                    }

                    var response = await Client.DeleteAsync($"api/terapie/rimuovi/{id}");

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        TempData["ErrorMessage"] = $"Errore durante l'eliminazione: {response.StatusCode} - {error}";
                    }

                    return RedirectToAction("Index");
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

        /// GET tutte le terapie:
        [HttpGet]
        public async Task<IActionResult> TerapieDiOggi()
        {
            
            var ruolo = HttpContext.Session.GetString("Ruolo");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            if (!matricola.HasValue || string.IsNullOrEmpty(password) || !idReparto.HasValue)
            {
                TempData["LoginError"] = "Sessione scaduta o dati utente mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            try
            {
                // Autenticazione Basic
                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                // Chiamata all'API
                var response = await Client.GetAsync($"api/terapie/oggi/{idReparto}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var terapie = JsonConvert.DeserializeObject<List<Terapia>>(json) ?? new List<Terapia>();

                    // Eventuale visualizzazione dei pazienti associati
                    var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                    Dictionary<int, string> pazientiMap = new Dictionary<int, string>();

                    if (responsePazienti.IsSuccessStatusCode)
                    {
                        var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                        var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                        pazientiMap = pazienti.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");
                    }

                    ViewBag.PazientiMapPerTabella = pazientiMap;
                    ViewBag.RuoloUtente = ruolo;
                    ViewBag.MatricolaMedico = matricola;

                    return View("TerapieDiOggi", terapie);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Errore nel recupero di tutte le terapie: {StatusCode} - {Error}", response.StatusCode, error);
                    TempData["ErrorMessage"] = $"Errore nel caricamento delle terapie: {response.StatusCode} - {error}";
                    return RedirectToAction("Index");
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

    }
}