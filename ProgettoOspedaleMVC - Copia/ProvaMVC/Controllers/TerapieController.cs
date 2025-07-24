
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ProvaMVC.Models;
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
            Client.BaseAddress = new Uri("http://localhost:5002");
            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        [HttpGet]
        public async Task<IActionResult> Terapie()
        {
            
            var ruolo = HttpContext.Session.GetString("Ruolo");
            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var idReparto = HttpContext.Session.GetInt32("Reparto");


            
            ViewBag.RuoloUtente = ruolo;
            ViewBag.MatricolaMedico = matricola ?? 0;

            List<Terapia> terapie = new List<Terapia>();
            List<Paziente> pazientiDisponibili = new List<Paziente>();
            Dictionary<int, string> pazientiMapPerTabella = new Dictionary<int, string>();

            try
            {
                
                if (matricola.HasValue && !string.IsNullOrEmpty(password))
                {
                    string authString = $"{matricola}:{password}";
                    string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                    Client.DefaultRequestHeaders.Authorization = null; 
                    Client.DefaultRequestHeaders.Add("Authorization", "Basic " + base64Token); 
                }
                else
                {
                    _logger.LogWarning("Matricola o Password sessione non presenti per autenticazione API.");
                    TempData["ErrorMessage"] = "Errore di autenticazione API. Effettuare nuovamente il login.";
                    return RedirectToAction("Login", "Utenti");
                }


                
                var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                if (responsePazienti.IsSuccessStatusCode)
                {
                    var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    pazientiDisponibili = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                    
                    pazientiMapPerTabella = pazientiDisponibili.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");

                    ViewBag.PazientiDisponibili = pazientiDisponibili;
                    ViewBag.PazientiMapPerTabella = pazientiMapPerTabella; 
                }
                else
                {
                    var errorPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    _logger.LogError("Errore API nel recupero pazienti per Terapie (GET): {StatusCode} - {Error}", responsePazienti.StatusCode, errorPazienti);
                    TempData["ErrorMessage"] = $"Impossibile caricare i pazienti del reparto: {errorPazienti}";
                }
                
                var responseTerapie = await Client.GetAsync($"api/Terapie/reparto/{idReparto}");
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
                    var errorTerapie = await responseTerapie.Content.ReadAsStringAsync();
                    _logger.LogError("Errore API nel recupero terapie per Terapie (GET): {StatusCode} - {Error}", responseTerapie.StatusCode, errorTerapie);
                    TempData["ErrorMessage"] = $"Impossibile caricare le terapie: {errorTerapie}";
                }
            
                 
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

        
        [HttpGet]
        public async Task<IActionResult> Assegna()
        {
            try { 

            var ruolo = HttpContext.Session.GetString("Ruolo");

            var matricola = HttpContext.Session.GetInt32("Matricola");
            var password = HttpContext.Session.GetString("Password");
            var reparto = HttpContext.Session.GetInt32("Reparto");

            
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

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assegna(Terapia nuovaTerapia)
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

            
            nuovaTerapia.MatricolaMedico = matricola.Value;

            if (!ModelState.IsValid)
            {
                
                await RepopulateViewBagData(matricola.Value, password, idReparto.Value, ruolo);
                TempData["ErrorMessage"] = "Errore di validazione nel form di assegnazione terapia. Controlla i campi.";
                return View(nuovaTerapia); 
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
                    return RedirectToAction("Index");
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
                    
                    await RepopulateViewBagData(matricola.Value, password, idReparto.Value, ruolo);
                    return View(nuovaTerapia);
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


        
        private async Task RepopulateViewBagData(int matricola, string? password, int idReparto, string? ruolo)
        {
           
            try
            {
                
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

                
                var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                if (responsePazienti.IsSuccessStatusCode)
                {
                    var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    var pazientiList = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                    ViewBag.PazientiDisponibili = pazientiList;
                    
                    ViewBag.PazientiMapPerTabella = pazientiList.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");
                }
                else
                {
                    var errorContent = await responsePazienti.Content.ReadAsStringAsync();
                    _logger.LogError("Errore nel ripopolamento pazienti (helper): {StatusCode} - {Error}", responsePazienti.StatusCode, errorContent);
                    ViewBag.PazientiDisponibili = new List<Paziente>(); 
                    ViewBag.PazientiMapPerTabella = new Dictionary<int, string>(); 
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eccezione durante il ripopolamento dei dati ViewBag.");
                ViewBag.PazientiDisponibili = new List<Paziente>();
                ViewBag.PazientiMapPerTabella = new Dictionary<int, string>();
            }

        }

        
        private async Task<List<Terapia>> GetExistingTerapie(int matricola, string? password, int idReparto)
        {

            List<Terapia> terapie = new List<Terapia>();
            Dictionary<int, string> pazientiMapPerTabella = new Dictionary<int, string>();

            try
            {
                
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

                
                var responsePazienti = await Client.GetAsync($"api/pazienti/reparto/{idReparto}");
                if (responsePazienti.IsSuccessStatusCode)
                {
                    var jsonPazienti = await responsePazienti.Content.ReadAsStringAsync();
                    var pazientiList = JsonConvert.DeserializeObject<List<Paziente>>(jsonPazienti) ?? new List<Paziente>();
                    
                    pazientiMapPerTabella = pazientiList.ToDictionary(p => p.ID, p => $"{p.Nome} {p.Cognome} ({p.CF})");
                }
                else
                {
                    _logger.LogError("Errore nel recupero pazienti per GetExistingTerapie (helper).");
                }

                
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
                
                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                
                var response = await Client.GetAsync($"api/terapie/oggi/{idReparto}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var terapie = JsonConvert.DeserializeObject<List<Terapia>>(json) ?? new List<Terapia>();

                   
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