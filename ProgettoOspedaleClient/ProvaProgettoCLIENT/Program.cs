using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestSharp;

namespace ClientPazienti
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new RestClient("http://localhost:5099");

            bool isLoggedIn = false;
            UtenteLoggato loggato = new UtenteLoggato { };

            while (true)
            {
                if (!isLoggedIn)
                {
                    Console.Clear();
                    Console.WriteLine("== Gestione Pazienti ==");
                    Console.WriteLine("1. Login");
                    Console.WriteLine("2. Registrazione");
                    Console.WriteLine("5. Esci");
                    Console.Write("Scegli un'opzione: ");
                    var scelta = Console.ReadLine();


                    switch (scelta)
                    {
                        case "1":
                            loggato = await Login(client);
                            if (loggato.Mat != 0)
                            {
                                isLoggedIn = true;
                            }
                            break;
                        case "2":
                            await Register(client);
                            break;
                        case "5":
                            return;
                        default:
                            Console.WriteLine("Scelta non valida!");
                            break;
                    }

                    Console.WriteLine("\nPremi un tasto per continuare...");
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine("\n Chiusura Programma");
                    return;
                }
            }
        }

        public static async Task<UtenteLoggato> Login(RestClient client)
        {
            int matricola;
            do
            {
                Console.Write("Matricola: ");
                string stringa = Console.ReadLine();
                if (int.TryParse(stringa, out matricola)) break;
                Console.WriteLine("Matricola non valida. Inserisci solo numeri.");
            } while (true);
            Console.Write("Password: ");
            string password = Console.ReadLine();

            var utente = new
            {
                Matricola =  matricola,
                Password = password,
            };

            var request = new RestRequest("api/utenti/login", Method.Post);
            request.AddJsonBody(utente);

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                Console.WriteLine("Login fallito.");
                return new UtenteLoggato { };
            }

            var parsed = JsonConvert.DeserializeObject<UtenteLoggato>(response.Content);
            Console.WriteLine("Login avvenuto con successo.");
            return parsed;
        }

        public static async Task Register(RestClient client)
        {

            Console.WriteLine("Inserisci nome:");
            string nome = Console.ReadLine();

            Console.WriteLine("Inserisci cognome:");
            string cognome = Console.ReadLine();

            Console.WriteLine("Inserisci password:");
            string password = Console.ReadLine();

            Console.WriteLine("Inserisci CF:");
            string codfiscale = Console.ReadLine();
            while (codfiscale.Length != 16)
            {
                Console.WriteLine("CF non corretto!");
                codfiscale = Console.ReadLine();
            }

            string ruolo = "";
            Console.WriteLine("Ruolo: ");
            Console.WriteLine("1. Medico");
            Console.WriteLine("2. Infermiere");
            Console.Write("Scegli un'opzione: ");
            string scelta = Console.ReadLine();
            switch (scelta)
            {
                case "1":
                    ruolo = "Medico";
                    break;
                case "2":
                    ruolo = "Infermiere";
                    break;
                default:
                    Console.WriteLine("Scelta non valida!");
                    break;
            }

            string reparto = "";
            Console.WriteLine("Reparti: ");
            var getReparti = new RestRequest("api/reparti", Method.Get);
            var resReparti = await client.ExecuteAsync(getReparti);
            if (resReparti.IsSuccessful && !string.IsNullOrEmpty(resReparti.Content))
            {
                var reparti = JsonConvert.DeserializeObject<List<Reparto>>(resReparti.Content);
                for (int i=0; i<reparti.Count; i++)
                {
                    Console.WriteLine((i + 1) + ". " + reparti[i].Nome);
                }
                Console.WriteLine("Seleziona il tuo reparto: ");
                if (int.TryParse(Console.ReadLine(), out int scelta2) && scelta2 >= 1 && scelta2 <= reparti.Count)
                    reparto = reparti[scelta2 - 1].Nome;
                else
                {
                    Console.WriteLine("Scelta non valida.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Errore nel recupero dei reparti dal server.");
                return;
            }

            var nuovoUtente = new
            {
                Password = password,
                CF = codfiscale,
                Nome = nome,
                Cognome = cognome,
                Ruolo = ruolo,
                NomeReparto = reparto
            };

            var postRequest = new RestRequest("api/utenti/registrazione", Method.Post);
            postRequest.AddJsonBody(nuovoUtente);

            var postResponse = await client.ExecuteAsync(postRequest);

            Console.WriteLine("Registrazione avvenuta con successo! La tua matricola è:");
            Console.WriteLine(postResponse.Content);
        }

    }

    public class UtenteLoggato
    {
        public int Mat { get; set; }
        public string RuoloMat { get; set; }
        public int Reparto { get; set; }

    }

    public class Utente
    {
        [Key]
        public int Matricola { get; set; }
        public string Password { get; set; }
        public string CF{ get; set; }
        public string Nome { get; set; }
        public string Cognome { get; set; }
        public string Ruolo { get; set; }
        public int IDReparto { get; set; }
    }

    public class LoginData
    {
        public int Matricola { get; set; }
        public string Password { get; set; }
    }

    public class Reparto
    {
        [Key]
        public string Nome { get; set; }
        public int NumeroLetti { get; set; }
    }
}
