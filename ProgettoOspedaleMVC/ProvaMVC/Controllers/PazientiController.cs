using Microsoft.AspNetCore.Mvc;               // Supporto per controller MVC
using Newtonsoft.Json;                        // Per (de)serializzare JSON
using ProvaMVC.Models;                        // Modelli dell’applicazione
using System.Net.Http.Headers;                // Gestione headers HTTP
using System.Text;                            // Operazioni su stringhe/encoding
using System.Net;                             // Per codici HTTP
using System.Net.Sockets;                     // Per gestire errori di rete

namespace ProvaMVC.Controllers
{
    public class PazientiController : Controller
    {
        private readonly HttpClient _client;                        // Client HTTP per chiamate API esterne
        private readonly ILogger<PazientiController> _logger;       // Logger per errori ed info

        // Costruttore che riceve logger e client HTTP
        public PazientiController(ILogger<PazientiController> logger, HttpClient client)
        {
            _logger = logger;
            _client = client;
            _client.BaseAddress = new Uri("http://localhost:5002");     // URL base per API (manager)
        }

        [HttpGet]
        public async Task<IActionResult> Pazienti()
        {
            // Recupero matricola e reparto dalla sessione corrispondente
            int? matricolaInt = HttpContext.Session.GetInt32("Matricola");
            var idReparto = HttpContext.Session.GetInt32("Reparto");

            // Se matricola non è presente (sessione scaduta), forza login
            if (matricolaInt == null)
            {
                TempData["LoginError"] = "Sessione scaduta o credenziali mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            string matricola = matricolaInt.Value.ToString();

            string? password = HttpContext.Session.GetString("Password");

            if (string.IsNullOrEmpty(matricola) || string.IsNullOrEmpty(password))  // Se matricola o password non valide, forza login
            {
                TempData["LoginError"] = "Sessione scaduta o credenziali mancanti. Effettuare nuovamente il login.";
                return RedirectToAction("Login", "Utenti");
            }

            try
            {


                string authString = $"{matricola}:{password}";                           // Crea stringa di autenticazione in formato "matricola:password"
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));    // Codifica in Base64 per Basic Auth

                _client.DefaultRequestHeaders.Authorization = null;
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);  // Imposta header Authorization con schema Basic

                var response = await _client.GetAsync($"api/pazienti/reparto/{idReparto}");         // Faccio una chiamata GET per ottenere i pazienti di un certo reparto

                if (response.IsSuccessStatusCode)   // controllo se il server risponde correttamente (codice 200)
                {
                    // leggo contenuto del json dalla risposta, deserializzo e passo la lista di pazienti alla view tramite viewbag
                    var result = await response.Content.ReadAsStringAsync();
                    var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(result)!;

                    ViewBag.Pazienti = pazienti;
                    return View("Pazienti");
                }
                else
                {
                    // nel caso di errore leggo messaggio e lo registro nel log
                    var errore = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API returned an error: {StatusCode} - {ErrorMessage}", response.StatusCode, errore);
                    TempData["LogError"] = $"Errore durante il recupero dei pazienti: {response.StatusCode} - {errore}";
                    // se l'errore è di autorizzazione forzo il login
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        TempData["LoginError"] = "Accesso non autorizzato. Effettuare nuovamente il login.";
                        return RedirectToAction("Login", "Utenti");
                    }

                    return View("Pazienti");
                }
            }
            catch (HttpRequestException)
            {
                return RedirectToAction("HttpError", "Home", new { statusCode = 503 });         // Errore nella chiamata API
            }

            catch (SocketException)
            {
                return RedirectToAction("HttpError", "Home", new { statusCode = 503 });         // Errore di rete più basso livello
            }
            catch (Exception ex)
            {
                TempData["ServerMessage"] = "Errore imprevisto: " + ex.Message;
                return RedirectToAction("HttpError", "Home", new { statusCode = 500 });         // Qualsiasi altro errore non gestito specificamente
            }

        }

        [HttpGet]   // Metodo GET per mostrare richiesta di trasferimento paziente
        public async Task<IActionResult> RichiestaTrasferimentoPaziente()
        {
            try
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

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico_infermiere");     // fa una chiamata api verso il manager che verrà inoltrata al server per invocare questo metodo che controlla se l'utente che stiamo utilizzando è un medico un infermiere (solo in questi 2 casi potrà accedere al form per richiedere il trasferimento del paziente)
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });   // ci riporta alla pagina HttpError.cshtml che usiamo per mostrare i codici di errore (es. 400 403 500 etc) + il messaggio di errore proveniente dal server
                }

                // recupera la lista dei pazienti nel reparto corrente e la lista di tutti i reparti
                var pazientiResponse = await _client.GetAsync($"api/pazienti/reparto/{repartoId}");
                var repartiResponse = await _client.GetAsync("api/reparti");

                if (!pazientiResponse.IsSuccessStatusCode || !repartiResponse.IsSuccessStatusCode)
                {
                    if (!pazientiResponse.IsSuccessStatusCode)  // nel caso il server non risponda con 200 (risposta corretta) andiamo a salvare la risposta del server per poi stamparla nella pagina HttpError
                    {
                        TempData["ServerMessage"] = "Errore nella modifica dei dati personali " + await pazientiResponse.Content.ReadAsStringAsync();
                        return RedirectToAction("HttpError", "Home", new { statusCode = (int)pazientiResponse.StatusCode });
                    }

                    TempData["Error"] = "Errore nel caricamento dei dati per il trasferimento.";
                    return RedirectToAction("Index", "Home");
                }

                // ricevo i json in risposta e deserializzo
                var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
                var repartiJson = await repartiResponse.Content.ReadAsStringAsync();

                var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(pazientiJson);
                var reparti = JsonConvert.DeserializeObject<List<Reparto>>(repartiJson);

                //passo i dati alla view
                ViewBag.Pazienti = pazienti;
                ViewBag.Reparti = reparti;

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

        [HttpPost]  // metodo POST per inviare la richiesta di trasferimento paziente quando verrà premuto il pulsante "Conferma Trasferimento"
        public async Task<IActionResult> RichiestaTrasferimentoPaziente(int idPaziente, int idRepartoDestinazione, int numeroLetto)
        {
            try
            {
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var payload = new   // costruisco un oggetto con memorizzati al suo interno l'ID del reparto selezionato ed il numero del letto selezionato
                {
                    IDReparto = idRepartoDestinazione,
                    NumeroLetto = numeroLetto
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PutAsync($"api/pazienti/trasferimento/{idPaziente}", content);     // POST da inviare al server (attraverso manager)

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ServerMessage"] = "Errore nel trasferimento: " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                return RedirectToAction("Index", "Home");

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

        [HttpGet]   // Metodo GET per ricvere il form necessario a pianificare un ricovero 
        public async Task<IActionResult> Prenota()
        {
            try
            {
                // ricavamiamo dalla sessione corrente matricola e password
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)  // nel caso ci siano problemi forziamo il login
                {
                    return RedirectToAction("Login", "Utenti");
                }

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico_infermiere"); // il form viene visualizzato solo se l'utente è un medico o un infermiere
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                }

                return View(new Paziente());    // ritorno la vista con il modello Paziente
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


        [HttpPost]  // metodo POST per pianificare il ricovero di un paziente con i dati che abbiamo inserito nel form mostrato dal metodo GET Prenota
        public async Task<IActionResult> Prenota(Paziente paziente)
        {
            try
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

                var jsonContent = JsonConvert.SerializeObject(paziente);    // passo i dati inseriti nell'oggetto di tipo Paziente e li converto in Json
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync("api/pazienti/aggiungi", content);   // faccio la chiamata al server (attraverso il manager) e mi salvo la risposta, in modo da controllare se è 200 (corretta) o c'è stato qualche errore

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Pazienti");
                }
                else
                {
                    TempData["ServerMessage"] = "Errore nella prenotazione " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
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

        [HttpGet]   // Metodo GET per ricvere il form necessario a ricoverare d'urgenza un paziente (data di ricovero di default impostata uguale ad oggi) 
        public async Task<IActionResult> RicoveroUrgente(int idPaziente)
        {
            try
            {
                var idReparto = HttpContext.Session.GetInt32("Reparto");
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (idReparto == null || matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico_infermiere");
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                }

                var response = await _client.GetAsync($"/api/reparti/lista_letti_liberi/{idReparto}");  // richiedo la lista dei letti liberi in uno specifico reparto (quello corrispondente al nostro utente) per farlo selezionare all'utente nel .cshtml

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ServerMessage"] = "Errore nel recupero letti " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                var content = await response.Content.ReadAsStringAsync();
                var lettiLiberi = JsonConvert.DeserializeObject<List<int>>(content);
                ViewBag.LettiLiberi = lettiLiberi;  // qui passo la lista alla vista

                return View("RicoveroUrgente", new Paziente    // restituisco alla vista un oggetto di tipo paziente in cui memorizzo l'id del paziente e l'id del reparto in cui siamo  
                {
                    ID = idPaziente,
                    IDReparto = idReparto.Value
                });
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

        [HttpPost]  // metodo POST per ricoverare un paziente con DataRicovero = oggi; essendo data di ricovero = oggi al paziente viene subito assegnato un letto 
        public async Task<IActionResult> RicoveroUrgente(Paziente paziente)
        {
            try
            {

                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                paziente.DataRicovero = DateOnly.FromDateTime(DateTime.Today);  // imposto in modo automatico DataRicovero

                var json = JsonConvert.SerializeObject(paziente);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync("api/pazienti/aggiungi", content);   // faccio la chiamata passandogli l'oggetto paziente

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Pazienti");
                }
                else
                {
                    TempData["ServerMessage"] = "Errore nel ricovero urgente " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
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

        [HttpGet]   // metodo GET per ottenere la lista dei pazienti con DataRicovero == oggi nel nostro reparto 
        public async Task<IActionResult> DaRicoverareOggi()
        {
            try
            {
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");
                var idReparto = HttpContext.Session.GetInt32("Reparto");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico");    // possiamo vedere questa lista solo se il nostro utente ha il ruolo di Medico
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                }

                // Otteniamo la lista di pazienti nel nostro reparto da ricoverare oggi. Se il server risponde in modo positivo la deserializziamo e successivamente la passeremo alla vista
                var pazientiResponse = await _client.GetAsync($"api/pazienti/da_ricoverare/{idReparto}/oggi");

                List<Paziente> pazienti = new List<Paziente>();

                if (pazientiResponse.IsSuccessStatusCode)
                {
                    var pazientiJson = await pazientiResponse.Content.ReadAsStringAsync();
                    pazienti = JsonConvert.DeserializeObject<List<Paziente>>(pazientiJson);
                }
                else
                {
                    TempData["ServerMessage"] = "Errore nel recupero dei pazienti " + await pazientiResponse.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)pazientiResponse.StatusCode });
                }


                var lettiResponse = await _client.GetAsync($"api/reparti/lista_letti_liberi/{idReparto}");  // ci salviamo anche la lista dei letti liberi (dato che il ricovero necessita della selezione di un letto)

                List<int> lettiLiberi = new List<int>();

                if (lettiResponse.IsSuccessStatusCode)
                {
                    var lettiJson = await lettiResponse.Content.ReadAsStringAsync();
                    lettiLiberi = JsonConvert.DeserializeObject<List<int>>(lettiJson);
                }
                else
                {
                    TempData["ServerMessage"] = "Errore nel recupero dei letti liberi " + await lettiResponse.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)lettiResponse.StatusCode });
                }

                ViewBag.LettiLiberi = lettiLiberi;  // passiamo alla vista la lista di letti liberi tramite una viewbag
                return View(pazienti);              // ritorniamo alla vista passandole la lista di pazienti

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


        [HttpPost]  // metodo POST per ricoverare i pazienti, passando il suo ID ed il NumeroLetto da assegnargli 
        public async Task<IActionResult> DaRicoverareOggi(int ID, int NumeroLetto)
        {
            try
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
                    return RedirectToAction("DaRicoverareOggi");
                }
                else
                {
                    TempData["ServerMessage"] = "Errore nel ricovero " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });

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


        [HttpGet]   // metodo GET per ottenere la lista dei pazienti, del nostro reparto, con DataDimissione == oggi 
        public async Task<IActionResult> DaDimettere()
        {
            try
            {
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");
                var idReparto = HttpContext.Session.GetInt32("Reparto");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico");
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                }

                var response = await _client.GetAsync($"api/pazienti/da_dimettere/{idReparto}/oggi");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {

                    TempData["Info"] = "Nessun paziente da dimettere oggi.";
                    return View(new List<Paziente>());
                }

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ServerMessage"] = "Errore nel recupero pazienti da dimettere " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                var json = await response.Content.ReadAsStringAsync();

                List<Paziente> pazienti;    // ottengo la lista dalla risposta proveniente dal server
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

                return View(pazienti);  // passo la lista di pazienti da dimettere oggi ritornando alla vista

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


        [HttpPost]  // metodo POST per dimettere un determinato paziente identificato dal suo ID 
        public async Task<IActionResult> DaDimettere(int ID)
        {
            try
            {
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var response = await _client.DeleteAsync($"api/pazienti/dimetti/{ID}"); // richiamo il metodo delete che elimina un paziente con un determinato ID 

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ServerMessage"] = "Errore nella dimissione " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                return RedirectToAction("DaDimettere");
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

        [HttpGet]   // Metodo GET per ottenere il form che permette di modificare i dati clinici di un paziente 
        public async Task<IActionResult> ModificaDatiMedici(int id)
        {
            try
            {
                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico");    // form accessibile solo se ruolo == medico
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                }

                var response = await _client.GetAsync($"api/pazienti/{id}");    // recupero i dati di un paziente per riempire il form con essi (li cambiamo se vogliamo modificarli)

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ServerMessage"] = "Errore nel caricamento del paziente " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                var json = await response.Content.ReadAsStringAsync();
                var paziente = JsonConvert.DeserializeObject<Paziente>(json);

                return View(paziente);

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

        [HttpPost]  // metodo POST per inviare i nuovi dati medici del paziente selezionato
        public async Task<IActionResult> InviaModificaDatiMedici(int IDPaziente, DateOnly? DataRicovero, DateOnly? DataDimissione, string? MotivoRicovero, string? Patologie, string? Allergie, string? AltreNote)
        {
            try
            {

                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var dati = new  // costruiamo questo oggetto con i dati modificati
                {
                    DataRicovero,
                    DataDimissione,
                    MotivoRicovero,
                    Patologie,
                    Allergie,
                    AltreNote
                };

                var content = new StringContent(JsonConvert.SerializeObject(dati), Encoding.UTF8, "application/json");

                var response = await _client.PutAsync($"api/pazienti/modifica_dati_medici/{IDPaziente}", content);  // passiamo i nuovi dati nella chiamata PUT

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ServerMessage"] = "Errore nella modifica dei dati medici " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }
                else return RedirectToAction("Pazienti");

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

        [HttpGet]   // modifica dati personali è uguale a modifica dati medici, cambia solo il fatto che anche gli infermieri possono accedere ed i dati che vengono modificati (anagrafici)
        public async Task<IActionResult> ModificaDatiPersonali(int id)
        {
            try
            {

                var matricola = HttpContext.Session.GetInt32("Matricola");
                var password = HttpContext.Session.GetString("Password");

                if (matricola == null || password == null)
                    return RedirectToAction("Login", "Utenti");

                string authString = $"{matricola}:{password}";
                string base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Token);

                var check = await _client.GetAsync("api/utenti/check_ruolo_medico_infermiere"); // form accessibile solo da medici ed infermieri
                if (!check.IsSuccessStatusCode)
                {
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)check.StatusCode });
                }

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
        public async Task<IActionResult> ModificaDatiPersonali(int IDPaziente, string? CF, string? Nome, string? Cognome, string? LuogoNascita, DateOnly? DataNascita)
        {
            try
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
                    TempData["ServerMessage"] = "Errore nella modifica dei dati personali: " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }
                else return RedirectToAction("Pazienti");

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

        [HttpGet]   // metodo GET per ottenere i pazienti da ricoverare (DataRicovero >= oggi)
        public async Task<IActionResult> ProssimiRicoveri()
        {
            try
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

                    TempData["ServerMessage"] = "Errore nel caricamento dei pazienti da ricoverare " + await response.Content.ReadAsStringAsync();
                    return RedirectToAction("HttpError", "Home", new { statusCode = (int)response.StatusCode });
                }

                var json = await response.Content.ReadAsStringAsync();
                var pazienti = JsonConvert.DeserializeObject<List<Paziente>>(json);

                return View(pazienti);
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