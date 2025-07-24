using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Net;
using System.Text;
using static System.Net.WebRequestMethods;

namespace ProxyManager.Controllers;

[ApiController]
[Route("{**path}")]  //cattura qualsiasi URL (anche con / annidati) e lo passa come parametro path. Serve a fare da catch-all proxy.
public class ProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public ProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(); //HttpClient viene creato da IHttpClientFactory
    }


    //Cattura tutte le richieste (GET, POST, PUT, DELETE) verso qualsiasi path (quello catturato da {**path })
    [HttpGet, HttpPost, HttpPut, HttpDelete]
    public async Task<IActionResult> ForwardRequest(string path)
    {
        var backendUrl = $"http://localhost:5099/{path}"; //punta a dove ascolta il server

        //crea una nuova richiesta http, copiando il metodo ricevuto e aggiunge l'url del server
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(Request.Method),
            RequestUri = new Uri(backendUrl)
        };

        //caso di PUT/POST in cui leggo il body e lo copio nella richiesta
        if (Request.ContentLength > 0)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        //Copia tutti gli header HTTP dalla richiesta originale a quella nuova.
        foreach (var header in Request.Headers)
        {
            //L'Authorization è un caso speciale e va gestito in modo diverso rispetto agli altri header Authorization non può essere aggiunto come stringa normale
            //La classe HttpClient(o HttpRequestMessage, che ne è parte) ha una proprietà specifica Authorization che richiede un oggetto di tipo AuthenticationHeaderValue.
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.Authorization =
                    System.Net.Http.Headers.AuthenticationHeaderValue.Parse(header.Value);
            }
            else
            {
                //provo ad aggiungere come header generali 
                if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    //se non riesce prova aggiungerli al content.header 
                    requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }
        }

        //invia la richiesta al server
        var response = await _httpClient.SendAsync(requestMessage);
        //legge la risposta
        var content = await response.Content.ReadAsStringAsync();

        //restituisce al client: statuscode + contenuto + content type
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }
}